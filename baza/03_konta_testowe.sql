-- WattPunkt Admin: konta testowe pracownikow (hasla w README.md projektu)
-- Uruchamiac po 01_utworz_baze.sql i 02_generuj_dane.sql. Skroty hasel policzono funkcja PBKDF2 (SHA-256, 100 000 iteracji).
USE WattPunktAdmin;
GO
SET NOCOUNT ON;
GO
INSERT INTO dbo.Pracownicy (Login, Imie, Nazwisko, Rola, HasloHash, HasloSol, Iteracje, Aktywny, WymagaZmianyHasla) VALUES
 ('admin',       N'Anna',      N'Zielińska',   N'Administrator',
  0x05C19A6CB10A633C10C8BEA81312C166C384B882377D89A53AA2FF7CFB9C37FA, 0xBA1843AFAD258BCB7EBD871C4CD5A3F0, 100000, 1, 0),
 ('pkowalski',   N'Piotr',     N'Kowalski',    N'Pracownik',
  0xA94BFC77EC2A31680DF7021FA915B016DC20689B4ECC0D6ADE2B360F65E0A5CE, 0x180853C463B56BCBD368CE7F4BD9D043, 100000, 1, 0),
 ('kwisniewska', N'Katarzyna', N'Wiśniewska',  N'Pracownik',
  0xB5E671BCD7511450BE2ABBBA3EC5A6E62EE2EB2B4B2FAB99C0F7CAB03377E2EE, 0xCE707C51AC50F87260E3854E15E1093A, 100000, 1, 0),
 ('mnowak',      N'Marta',     N'Nowak',       N'Pracownik',
  0xBEFBE3A44DB6FC588398C5441A116F4A139290232242B247E6D4F7D2C8D255F5, 0x1948602D9A2A439C62841FCAB8F7B753, 100000, 0, 0);
GO
SELECT Login, Imie + N' ' + Nazwisko AS Pracownik, Rola, Aktywny FROM dbo.Pracownicy ORDER BY PracownikID;
GO
