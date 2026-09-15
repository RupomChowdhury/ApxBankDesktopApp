using System;
using System.IO;
using System.Linq;
using ApexBank.Models;
using ApexBank.Services;
using ClosedXML.Excel;
using Xunit;

namespace Tests;

public class CardManagementAndEncryptionTests
{
    [Fact]
    public void EncryptionService_EncryptAndDecrypt_RoundTripsAccurately()
    {
        string pan = "4738 9201 5542 8829";
        string cvv = "749";

        string encPan = EncryptionService.Encrypt(pan);
        string encCvv = EncryptionService.Encrypt(cvv);

        // Verify format
        Assert.StartsWith("ENC:v1:", encPan);
        Assert.StartsWith("ENC:v1:", encCvv);
        Assert.NotEqual(pan, encPan);
        Assert.NotEqual(cvv, encCvv);

        // Verify decryption round-trip
        string decPan = EncryptionService.Decrypt(encPan);
        string decCvv = EncryptionService.Decrypt(encCvv);

        Assert.Equal(pan, decPan);
        Assert.Equal(cvv, decCvv);
    }

    [Fact]
    public void CardsSheet_IsCreatedAndStoresCredentialsEncryptedInExcel()
    {
        string testDbPath = Path.Combine(Path.GetTempPath(), $"ApexBank_CardsTest_{Guid.NewGuid():N}.xlsx");
        try
        {
            var db = new ExcelDatabaseService(testDbPath);

            // Read directly from Excel file on disk to inspect raw storage
            using (var workbook = new XLWorkbook(testDbPath))
            {
                Assert.True(workbook.TryGetWorksheet("Cards", out var wsCards));
                Assert.NotNull(wsCards);

                // Check header columns
                Assert.Equal("CardId", wsCards.Cell(1, 1).GetString());
                Assert.Equal("UserId", wsCards.Cell(1, 2).GetString());
                Assert.Equal("EncryptedCardNumber", wsCards.Cell(1, 5).GetString());
                Assert.Equal("EncryptedCvv", wsCards.Cell(1, 6).GetString());
                Assert.Equal("MaskedNumber", wsCards.Cell(1, 7).GetString());

                // Inspect Alexander's seeded card row (Row 2)
                var row = wsCards.Row(2);
                string cardId = row.Cell(1).GetString();
                string userId = row.Cell(2).GetString();
                string cardHolder = row.Cell(4).GetString();
                string rawEncNumber = row.Cell(5).GetString();
                string rawEncCvv = row.Cell(6).GetString();
                string masked = row.Cell(7).GetString();

                Assert.Equal("card_alex_01", cardId);
                Assert.Equal("usr_alex_01", userId);
                Assert.Equal("ALEXANDER CROSS", cardHolder);

                // Raw storage MUST be encrypted with ENC:v1: prefix and never plaintext
                Assert.StartsWith("ENC:v1:", rawEncNumber);
                Assert.StartsWith("ENC:v1:", rawEncCvv);
                Assert.DoesNotContain("4738 9201 5542 8829", rawEncNumber);
                Assert.DoesNotContain("749", rawEncCvv);
                Assert.Equal("4738 •••• •••• 8829", masked);

                // Decrypted data matches
                Assert.Equal("4738 9201 5542 8829", EncryptionService.Decrypt(rawEncNumber));
                Assert.Equal("749", EncryptionService.Decrypt(rawEncCvv));
            }

            // Verify GetCards decrypts correctly into memory
            var cards = db.GetCards("usr_alex_01");
            Assert.Single(cards);
            var alexCard = cards.First();
            Assert.Equal("4738 9201 5542 8829", alexCard.FullNumber);
            Assert.Equal("749", alexCard.Cvv);
            Assert.Equal("4738 •••• •••• 8829", alexCard.MaskedNumber);
            Assert.Equal("ALEXANDER CROSS", alexCard.CardHolder);
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (File.Exists(testDbPath))
            {
                try { File.Delete(testDbPath); } catch { }
            }
        }
    }

    [Fact]
    public void MultipleUsers_HaveIsolatedCards_WithUniqueNumbersAndHolders()
    {
        string testDbPath = Path.Combine(Path.GetTempPath(), $"ApexBank_MultiCard_{Guid.NewGuid():N}.xlsx");
        try
        {
            var db = new ExcelDatabaseService(testDbPath);
            var bankService = new BankService(db);

            // 1. Register Alice
            var regAlice = bankService.Register("Alice Smith", "alice", "alice@example.com", "Password@123");
            Assert.True(regAlice.Success);
            string aliceId = bankService.CurrentUser!.Id;
            var aliceCards = bankService.Cards.ToList();
            Assert.NotEmpty(aliceCards);
            var aliceCard = aliceCards.First();

            Assert.Equal("ALICE SMITH", aliceCard.CardHolder);
            Assert.StartsWith("4", aliceCard.FullNumber.Replace(" ", "")); // Valid Visa
            Assert.False(string.IsNullOrWhiteSpace(aliceCard.Cvv));

            // 2. Register Bob
            bankService.Logout();
            var regBob = bankService.Register("Bob Jones", "bob", "bob@example.com", "Password@456");
            Assert.True(regBob.Success);
            string bobId = bankService.CurrentUser!.Id;
            var bobCards = bankService.Cards.ToList();
            Assert.NotEmpty(bobCards);
            var bobCard = bobCards.First();

            Assert.Equal("BOB JONES", bobCard.CardHolder);
            Assert.NotEqual(aliceId, bobId);

            // 3. Verify Alice and Bob have COMPLETELY DISTINCT cards
            Assert.NotEqual(aliceCard.Id, bobCard.Id);
            Assert.NotEqual(aliceCard.FullNumber, bobCard.FullNumber);
            Assert.NotEqual(aliceCard.MaskedNumber, bobCard.MaskedNumber);

            // 4. Directly inspect Excel 'Cards' worksheet on disk
            using (var workbook = new XLWorkbook(testDbPath))
            {
                var wsCards = workbook.Worksheet("Cards");
                var allCardRows = wsCards.RowsUsed().Skip(1).ToList();

                var aliceRow = allCardRows.FirstOrDefault(r => r.Cell(2).GetString() == aliceId);
                var bobRow = allCardRows.FirstOrDefault(r => r.Cell(2).GetString() == bobId);

                Assert.NotNull(aliceRow);
                Assert.NotNull(bobRow);

                Assert.Equal("ALICE SMITH", aliceRow.Cell(4).GetString());
                Assert.Equal("BOB JONES", bobRow.Cell(4).GetString());

                // Both encrypted
                Assert.StartsWith("ENC:v1:", aliceRow.Cell(5).GetString());
                Assert.StartsWith("ENC:v1:", bobRow.Cell(5).GetString());
                Assert.NotEqual(aliceRow.Cell(5).GetString(), bobRow.Cell(5).GetString());
            }

            // 5. Re-login as Alice and verify isolation: Bob's card never leaks to Alice
            bankService.Logout();
            var loginAlice = bankService.Login("alice", "Password@123");
            Assert.True(loginAlice.Success);
            Assert.Single(bankService.Cards);
            Assert.Equal("ALICE SMITH", bankService.Card.CardHolder);
            Assert.Equal(aliceCard.FullNumber, bankService.Card.FullNumber);

            // 6. Re-login as Bob and verify isolation: Alice's card never leaks to Bob
            bankService.Logout();
            var loginBob = bankService.Login("bob", "Password@456");
            Assert.True(loginBob.Success);
            Assert.Single(bankService.Cards);
            Assert.Equal("BOB JONES", bankService.Card.CardHolder);
            Assert.Equal(bobCard.FullNumber, bankService.Card.FullNumber);
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (File.Exists(testDbPath))
            {
                try { File.Delete(testDbPath); } catch { }
            }
        }
    }

    [Fact]
    public void GenerateNewCard_CreatesDynamicCard_AndPersistsToExcelWithEncryption()
    {
        string testDbPath = Path.Combine(Path.GetTempPath(), $"ApexBank_GenCard_{Guid.NewGuid():N}.xlsx");
        try
        {
            var db = new ExcelDatabaseService(testDbPath);
            var bankService = new BankService(db);

            var reg = bankService.Register("Charlie Brown", "charlie", "charlie@bank.com", "Password@999");
            Assert.True(reg.Success);
            Assert.Single(bankService.Cards);

            var originalCard = bankService.Cards.First();

            // Act: Generate a new disposable virtual card
            var genResult = bankService.GenerateNewCard(brand: "VISA", tier: "Disposable Virtual");
            Assert.True(genResult.Success);
            Assert.NotNull(genResult.Card);
            Assert.Equal(2, bankService.Cards.Count);

            var newCard = genResult.Card;
            Assert.True(newCard.IsDisposable);
            Assert.Equal("CHARLIE BROWN", newCard.CardHolder);
            Assert.NotEqual(originalCard.FullNumber, newCard.FullNumber);
            Assert.Equal(newCard.Id, bankService.Card.Id); // Active card switched to newly created card

            // Verify persistence in Excel 'Cards' worksheet
            using (var workbook = new XLWorkbook(testDbPath))
            {
                var wsCards = workbook.Worksheet("Cards");
                var rows = wsCards.RowsUsed().Where(r => r.Cell(2).GetString() == bankService.CurrentUser!.Id).ToList();
                Assert.Equal(2, rows.Count);

                var newCardRow = rows.FirstOrDefault(r => r.Cell(1).GetString() == newCard.Id);
                Assert.NotNull(newCardRow);
                Assert.StartsWith("ENC:v1:", newCardRow.Cell(5).GetString());
                Assert.StartsWith("ENC:v1:", newCardRow.Cell(6).GetString());
                Assert.Equal(newCard.FullNumber, EncryptionService.Decrypt(newCardRow.Cell(5).GetString()));
            }

            // Verify audit log recorded in 'Logs' sheet
            var logs = db.GetLogs(bankService.CurrentUser!.Id);
            Assert.Contains(logs, l => l.Action == "CARD_GENERATED");
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (File.Exists(testDbPath))
            {
                try { File.Delete(testDbPath); } catch { }
            }
        }
    }

    [Fact]
    public void CardFreeze_UpdatesExcelSheetAndPreservesState()
    {
        string testDbPath = Path.Combine(Path.GetTempPath(), $"ApexBank_FreezeCard_{Guid.NewGuid():N}.xlsx");
        try
        {
            var db = new ExcelDatabaseService(testDbPath);
            var bankService = new BankService(db);

            bankService.Login("alexander", "Apex@2026");
            Assert.False(bankService.Card.IsFrozen);

            // Act: Toggle freeze
            bankService.ToggleCardFreeze();
            Assert.True(bankService.Card.IsFrozen);

            // Verify directly in Excel worksheet
            using (var workbook = new XLWorkbook(testDbPath))
            {
                var wsCards = workbook.Worksheet("Cards");
                var alexRow = wsCards.RowsUsed().First(r => r.Cell(1).GetString() == bankService.Card.Id);
                Assert.True(bool.Parse(alexRow.Cell(12).GetString()));
            }

            // Verify audit log
            var logs = db.GetLogs(bankService.CurrentUser!.Id);
            Assert.Contains(logs, l => l.Action == "CARD_FREEZE_TOGGLE" && l.Details.Contains("FROZEN"));

            // Act: Toggle freeze back to active
            bankService.ToggleCardFreeze();
            Assert.False(bankService.Card.IsFrozen);

            using (var workbook = new XLWorkbook(testDbPath))
            {
                var wsCards = workbook.Worksheet("Cards");
                var alexRow = wsCards.RowsUsed().First(r => r.Cell(1).GetString() == bankService.Card.Id);
                Assert.False(bool.Parse(alexRow.Cell(12).GetString()));
            }
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (File.Exists(testDbPath))
            {
                try { File.Delete(testDbPath); } catch { }
            }
        }
    }

    [Fact]
    public void VirtualCard_SecurityRuleToggles_RaisePropertyChangedAndPersistToExcel()
    {
        string testDbPath = Path.Combine(Path.GetTempPath(), $"ApexBank_CardRules_{Guid.NewGuid():N}.xlsx");
        try
        {
            var db = new ExcelDatabaseService(testDbPath);
            var bankService = new BankService(db);
            bankService.Login("alexander", "Apex@2026");

            var card = bankService.Card;
            Assert.NotNull(card);

            // Track property change events
            var changedProperties = new System.Collections.Generic.List<string>();
            card.PropertyChanged += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.PropertyName))
                    changedProperties.Add(e.PropertyName);
            };

            // Toggle Online Purchases off
            bankService.UpdateCardSecuritySettings(card.Id, online: false);
            Assert.False(card.OnlinePurchasesEnabled);
            Assert.False(card.AllowOnlinePurchases);
            Assert.Contains("OnlinePurchasesEnabled", changedProperties);
            Assert.Contains("AllowOnlinePurchases", changedProperties);

            // Toggle Contactless off
            bankService.UpdateCardSecuritySettings(card.Id, contactless: false);
            Assert.False(card.ContactlessEnabled);
            Assert.False(card.AllowContactless);
            Assert.Contains("ContactlessEnabled", changedProperties);
            Assert.Contains("AllowContactless", changedProperties);

            // Toggle International off
            bankService.UpdateCardSecuritySettings(card.Id, international: false);
            Assert.False(card.InternationalPaymentsEnabled);
            Assert.False(card.AllowInternational);
            Assert.Contains("InternationalPaymentsEnabled", changedProperties);
            Assert.Contains("AllowInternational", changedProperties);

            // Verify persistence in Excel worksheet
            using (var workbook = new XLWorkbook(testDbPath))
            {
                var wsCards = workbook.Worksheet("Cards");
                var row = wsCards.RowsUsed().First(r => r.Cell(1).GetString() == card.Id);
                Assert.False(bool.Parse(row.Cell(15).GetString())); // ContactlessEnabled
                Assert.False(bool.Parse(row.Cell(16).GetString())); // OnlinePurchasesEnabled
                Assert.False(bool.Parse(row.Cell(17).GetString())); // InternationalPaymentsEnabled
            }
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (File.Exists(testDbPath))
            {
                try { File.Delete(testDbPath); } catch { }
            }
        }
    }
}
