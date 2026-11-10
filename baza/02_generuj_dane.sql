-- WattPunkt Admin: generator danych transakcyjnych bazy WattPunktAdmin (klienci, plany, pojazdy, sesje ladowania, awarie)
-- Okres danych: 1.01.2024 do 31.12.2025. Uruchamiac zaraz po 01_utworz_baze.sql.
-- Kazda "losowa" liczba to skrot SHA-256 numeru wiersza (funkcja gen.Los), dlatego kolejne uruchomienie daje te same dane.
USE WattPunktAdmin;
GO
SET NOCOUNT ON;
IF EXISTS (SELECT 1 FROM dbo.Klienci)
BEGIN
    RAISERROR (N'Baza zawiera juz dane, najpierw uruchom ponownie 01_utworz_baze.sql.', 16, 1);
    SET NOEXEC ON;
END
GO
IF SCHEMA_ID(N'gen') IS NULL EXEC (N'CREATE SCHEMA gen');
GO
-- liczba pseudolosowa z przedzialu [0, 1) zalezna tylko od numeru @n i "ziarna" @k
CREATE OR ALTER FUNCTION gen.Los (@n bigint, @k int) RETURNS float
AS
BEGIN
    RETURN CAST(CAST(HASHBYTES('SHA2_256', CONCAT(@k, '|', @n)) AS binary(4)) AS bigint) / 4294967296.0;
END
GO
-- =====================================================================================
-- 1) Tabele pomocnicze: dni okresu, swieta, profile godzinowe, wagi miast
-- =====================================================================================
DROP TABLE IF EXISTS gen.Dni, gen.ProfilGodzin, gen.WagiMiast, gen.ProfilPojazdu;
CREATE TABLE gen.Dni (DzienNr int PRIMARY KEY, Data date NOT NULL UNIQUE, DzienTyg tinyint NOT NULL, Miesiac tinyint NOT NULL,
                      Swieto bit NOT NULL, Wyjazdowy float NOT NULL);
WITH n AS (SELECT TOP (731) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i FROM sys.all_objects),
swieta AS (SELECT CAST(d AS date) AS d FROM (VALUES
    ('2024-01-01'), ('2024-01-06'), ('2024-03-31'), ('2024-04-01'), ('2024-05-01'), ('2024-05-03'), ('2024-05-19'), ('2024-05-30'),
    ('2024-08-15'), ('2024-11-01'), ('2024-11-11'), ('2024-12-25'), ('2024-12-26'),
    ('2025-01-01'), ('2025-01-06'), ('2025-04-20'), ('2025-04-21'), ('2025-05-01'), ('2025-05-03'), ('2025-06-08'), ('2025-06-19'),
    ('2025-08-15'), ('2025-11-01'), ('2025-11-11'), ('2025-12-24'), ('2025-12-25'), ('2025-12-26')) AS v (d))
INSERT INTO gen.Dni
SELECT n.i, x.d, x.dt, MONTH(x.d),
       CASE WHEN x.d IN (SELECT d FROM swieta) THEN 1 ELSE 0 END,
       0.10 + CASE WHEN MONTH(x.d) IN (7, 8) THEN 0.10 ELSE 0 END
            + CASE WHEN x.dt IN (5, 6, 7) THEN 0.08 ELSE 0 END
            + CASE WHEN x.d IN (SELECT d FROM swieta) OR DATEADD(DAY, 1, x.d) IN (SELECT d FROM swieta) THEN 0.15 ELSE 0 END
FROM n CROSS APPLY (SELECT DATEADD(DAY, n.i - 1, CAST('2024-01-01' AS date)) AS d) a
       CROSS APPLY (SELECT a.d, CAST(DATEDIFF(DAY, '19000101', a.d) % 7 + 1 AS tinyint) AS dt) x;   -- 1 = poniedzialek

-- rozklad godzin rozpoczecia sesji dla typu lokalizacji (wagi dla godzin 0..23)
CREATE TABLE gen.ProfilGodzin (Typ nvarchar(20), Godzina tinyint, CumOd float, CumDo float);
WITH p AS (SELECT * FROM (VALUES
    (N'osiedle',          '1,0,0,0,0,1,2,3,3,2,2,2,2,2,2,3,5,8,10,10,9,7,4,2'),
    (N'biurowiec',        '0,0,0,0,0,0,2,8,12,10,6,4,4,4,3,3,3,2,1,1,0,0,0,0'),
    (N'centrum handlowe', '0,0,0,0,0,0,0,0,1,3,6,8,9,9,9,9,9,9,8,6,4,2,0,0'),
    (N'parking miejski',  '0,0,0,0,0,0,1,3,6,8,8,8,8,8,8,8,7,6,4,3,2,1,0,0'),
    (N'hotel',            '1,0,0,0,0,0,1,2,2,1,1,1,1,1,2,3,4,6,8,9,8,6,4,2'),
    (N'MOP',              '1,1,1,1,1,1,2,4,6,7,8,9,9,9,9,9,9,8,7,6,4,3,2,1')) AS v (Typ, Wagi)),
w AS (SELECT p.Typ, CAST(s.ordinal - 1 AS tinyint) AS Godzina, CAST(s.value AS float) AS Waga
      FROM p CROSS APPLY STRING_SPLIT(p.Wagi, ',', 1) s)
INSERT INTO gen.ProfilGodzin
SELECT Typ, Godzina,
       (SUM(Waga) OVER (PARTITION BY Typ ORDER BY Godzina) - Waga) / SUM(Waga) OVER (PARTITION BY Typ),
       SUM(Waga) OVER (PARTITION BY Typ ORDER BY Godzina) / SUM(Waga) OVER (PARTITION BY Typ)
FROM w WHERE Waga > 0;

-- udzial miast w liczbie klientow indywidualnych
CREATE TABLE gen.WagiMiast (MiejscowoscID int, CumOd float, CumDo float);
WITH w AS (SELECT m.MiejscowoscID, v.Waga FROM (VALUES
    (N'Warszawa', 30), (N'Kraków', 11), (N'Wrocław', 9), (N'Poznań', 7), (N'Gdańsk', 6), (N'Łódź', 6), (N'Katowice', 4),
    (N'Gdynia', 3), (N'Szczecin', 3.5), (N'Bydgoszcz', 2.5), (N'Lublin', 3), (N'Białystok', 2), (N'Rzeszów', 2), (N'Toruń', 2),
    (N'Gliwice', 2), (N'Olsztyn', 1.5), (N'Kielce', 1.5), (N'Opole', 1), (N'Zielona Góra', 1), (N'Radom', 1.5)) AS v (Nazwa, Waga)
    JOIN dbo.Miejscowosci m ON m.Nazwa = v.Nazwa)
INSERT INTO gen.WagiMiast
SELECT MiejscowoscID, (SUM(Waga) OVER (ORDER BY MiejscowoscID) - Waga) / SUM(Waga) OVER (), SUM(Waga) OVER (ORDER BY MiejscowoscID) / SUM(Waga) OVER ()
FROM w;
GO
-- =====================================================================================
-- 2) Klienci: 2400 klientow indywidualnych i 30 firm z flotami
-- =====================================================================================
DROP TABLE IF EXISTS #K;
CREATE TABLE #K (Tmp int PRIMARY KEY, TypKlienta nvarchar(15), Nazwa nvarchar(80), MiejscowoscID int, DataRejestracji date,
                 PlanStart int, DataPlus date NULL, DataPowrotu date NULL, Kategoria nvarchar(10), LiczbaAut int);
WITH n AS (SELECT TOP (2400) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i FROM sys.all_objects a CROSS JOIN sys.all_objects b),
imiona AS (SELECT * FROM (VALUES
    (1, N'Anna', N'Piotr'), (2, N'Maria', N'Krzysztof'), (3, N'Katarzyna', N'Andrzej'), (4, N'Małgorzata', N'Tomasz'), (5, N'Agnieszka', N'Paweł'),
    (6, N'Barbara', N'Jan'), (7, N'Ewa', N'Michał'), (8, N'Magdalena', N'Marcin'), (9, N'Joanna', N'Jakub'), (10, N'Aleksandra', N'Adam'),
    (11, N'Monika', N'Łukasz'), (12, N'Natalia', N'Mateusz'), (13, N'Karolina', N'Marek'), (14, N'Julia', N'Grzegorz'), (15, N'Zofia', N'Wojciech'),
    (16, N'Marta', N'Kamil'), (17, N'Paulina', N'Dariusz'), (18, N'Dorota', N'Rafał'), (19, N'Beata', N'Bartosz'), (20, N'Justyna', N'Maciej')) AS v (Nr, K, M)),
nazwiska AS (SELECT * FROM (VALUES
    (1, N'Nowak', N'Nowak'), (2, N'Kowalska', N'Kowalski'), (3, N'Wiśniewska', N'Wiśniewski'), (4, N'Wójcik', N'Wójcik'), (5, N'Kowalczyk', N'Kowalczyk'),
    (6, N'Kamińska', N'Kamiński'), (7, N'Lewandowska', N'Lewandowski'), (8, N'Zielińska', N'Zieliński'), (9, N'Szymańska', N'Szymański'), (10, N'Woźniak', N'Woźniak'),
    (11, N'Dąbrowska', N'Dąbrowski'), (12, N'Kozłowska', N'Kozłowski'), (13, N'Jankowska', N'Jankowski'), (14, N'Mazur', N'Mazur'), (15, N'Wojciechowska', N'Wojciechowski'),
    (16, N'Kwiatkowska', N'Kwiatkowski'), (17, N'Krawczyk', N'Krawczyk'), (18, N'Kaczmarek', N'Kaczmarek'), (19, N'Piotrowska', N'Piotrowski'), (20, N'Grabowska', N'Grabowski'),
    (21, N'Zając', N'Zając'), (22, N'Pawłowska', N'Pawłowski'), (23, N'Michalska', N'Michalski'), (24, N'Król', N'Król'), (25, N'Wieczorek', N'Wieczorek'),
    (26, N'Jabłońska', N'Jabłoński'), (27, N'Wróbel', N'Wróbel'), (28, N'Nowakowska', N'Nowakowski'), (29, N'Majewska', N'Majewski'), (30, N'Olszewska', N'Olszewski')) AS v (Nr, K, M)),
pierwsza AS (SELECT MiejscowoscID, MIN(DataUruchomienia) AS Otwarcie FROM dbo.Stacje GROUP BY MiejscowoscID),
baza AS (
    SELECT n.i, w.MiejscowoscID,
           CASE WHEN gen.Los(n.i, 102) < 0.5 THEN 1 ELSE 0 END AS Kobieta,
           CAST(gen.Los(n.i, 103) * 20 AS int) + 1 AS NrImienia, CAST(gen.Los(n.i, 104) * 30 AS int) + 1 AS NrNazwiska,
           r.Rmin
    FROM n
    JOIN gen.WagiMiast w ON gen.Los(n.i, 101) >= w.CumOd AND gen.Los(n.i, 101) < w.CumDo
    JOIN pierwsza p ON p.MiejscowoscID = w.MiejscowoscID
    CROSS APPLY (SELECT CASE WHEN DATEADD(DAY, -60, p.Otwarcie) < '2022-06-01' THEN CAST('2022-06-01' AS date) ELSE DATEADD(DAY, -60, p.Otwarcie) END AS Rmin) r
),
daty AS (
    SELECT b.*, DATEADD(DAY, CAST(DATEDIFF(DAY, b.Rmin, '2025-12-20') * SQRT(gen.Los(b.i, 105)) AS int), b.Rmin) AS DataRej
    FROM baza b
),
plany AS (
    SELECT d.*, CASE WHEN gen.Los(d.i, 106) < 0.18 THEN 2 ELSE 1 END AS PlanStart,
           DATEDIFF(DAY, DATEADD(DAY, 30, d.DataRej), '2025-12-31') AS Zakres
    FROM daty d
)
INSERT INTO #K (Tmp, TypKlienta, Nazwa, MiejscowoscID, DataRejestracji, PlanStart, DataPlus, Kategoria, LiczbaAut)
SELECT p.i, N'indywidualny', CASE WHEN p.Kobieta = 1 THEN im.K + N' ' + nz.K ELSE im.M + N' ' + nz.M END,
       p.MiejscowoscID, p.DataRej, p.PlanStart,
       CASE WHEN p.PlanStart = 1 AND p.Zakres > 0 AND gen.Los(p.i, 107) < 0.30
            THEN DATEADD(DAY, 30 + CAST(p.Zakres * gen.Los(p.i, 108) AS int), p.DataRej) END,
       N'ind', CASE WHEN gen.Los(p.i, 111) < 0.06 THEN 2 ELSE 1 END
FROM plany p JOIN imiona im ON im.Nr = p.NrImienia JOIN nazwiska nz ON nz.Nr = p.NrNazwiska;

-- czesc klientow z planem Plus wraca pozniej do planu Start
UPDATE k SET DataPowrotu = DATEADD(DAY, 60 + CAST(x.Zakres * gen.Los(k.Tmp, 110) AS int), x.Od)
FROM #K k
CROSS APPLY (SELECT CASE WHEN k.PlanStart = 2 THEN k.DataRejestracji ELSE k.DataPlus END AS Od) o
CROSS APPLY (SELECT o.Od, DATEDIFF(DAY, DATEADD(DAY, 60, o.Od), '2025-12-31') AS Zakres) x
WHERE o.Od IS NOT NULL AND x.Zakres > 0 AND gen.Los(k.Tmp, 109) < 0.10;

INSERT INTO #K (Tmp, TypKlienta, Nazwa, MiejscowoscID, DataRejestracji, PlanStart, Kategoria, LiczbaAut)
SELECT 10000 + f.Nr, N'firmowy', f.Nazwa, m.MiejscowoscID, f.DataRej, 3, f.Kategoria, f.Aut
FROM (VALUES
    (1, N'Eko Taxi Warszawa sp. z o.o.', N'Warszawa', '2023-02-01', N'taxi', 40),
    (2, N'Kurier Miejski Wisła sp. z o.o.', N'Warszawa', '2023-05-15', N'kurier', 30),
    (3, N'Zielony Wynajem sp. z o.o.', N'Warszawa', '2024-03-04', N'firma', 25),
    (4, N'Serwis Wind Dźwigtech sp. z o.o.', N'Warszawa', '2024-09-16', N'firma', 12),
    (5, N'Catering Smaczny Dzień sp. z o.o.', N'Warszawa', '2025-04-07', N'firma', 8),
    (6, N'Krakowskie Taxi Elektro sp. z o.o.', N'Kraków', '2023-03-20', N'taxi', 30),
    (7, N'Wawel Kurier sp. z o.o.', N'Kraków', '2024-05-13', N'kurier', 15),
    (8, N'MedTransport Południe sp. z o.o.', N'Kraków', '2025-01-13', N'firma', 10),
    (9, N'Odra Taxi sp. z o.o.', N'Wrocław', '2023-01-16', N'taxi', 25),
    (10, N'Dolnośląski Serwis Klimatyzacji sp. z o.o.', N'Wrocław', '2024-06-10', N'firma', 8),
    (11, N'Łódzkie Przewozy Osobowe sp. z o.o.', N'Łódź', '2023-04-24', N'taxi', 18),
    (12, N'Fabryczna Logistyka sp. z o.o.', N'Łódź', '2024-10-07', N'kurier', 10),
    (13, N'Poznański Wynajem Aut sp. z o.o.', N'Poznań', '2023-08-21', N'firma', 20),
    (14, N'Warta Kurier sp. z o.o.', N'Poznań', '2024-02-19', N'kurier', 12),
    (15, N'Bałtyk Taxi sp. z o.o.', N'Gdańsk', '2023-05-08', N'taxi', 20),
    (16, N'Portowy Serwis Techniczny sp. z o.o.', N'Gdynia', '2024-04-15', N'firma', 8),
    (17, N'Pomorska Opieka Domowa sp. z o.o.', N'Gdańsk', '2025-02-03', N'firma', 10),
    (18, N'Szczecińskie Taxi Zachód sp. z o.o.', N'Szczecin', '2024-01-08', N'taxi', 12),
    (19, N'Brda Logistyka sp. z o.o.', N'Bydgoszcz', '2024-03-11', N'kurier', 8),
    (20, N'Kopernik Serwis sp. z o.o.', N'Toruń', '2024-08-05', N'firma', 6),
    (21, N'Lubelski Kurier Ekspres sp. z o.o.', N'Lublin', '2024-01-29', N'kurier', 10),
    (22, N'Podlaskie Przewozy sp. z o.o.', N'Białystok', '2024-06-17', N'taxi', 6),
    (23, N'Śląskie Taxi Elektryczne sp. z o.o.', N'Katowice', '2023-02-27', N'taxi', 22),
    (24, N'Górnośląski Serwis Energetyczny sp. z o.o.', N'Gliwice', '2025-04-14', N'firma', 8),
    (25, N'Rzeszowski Wynajem Aut sp. z o.o.', N'Rzeszów', '2024-11-18', N'firma', 8),
    (26, N'Warmia Kurier sp. z o.o.', N'Olsztyn', '2025-02-24', N'kurier', 5),
    (27, N'Świętokrzyska Opieka Medyczna sp. z o.o.', N'Kielce', '2024-08-26', N'firma', 6),
    (28, N'Opolskie Usługi Komunalne sp. z o.o.', N'Opole', '2025-06-02', N'firma', 6),
    (29, N'Lubuski Serwis Grzewczy sp. z o.o.', N'Zielona Góra', '2025-09-01', N'firma', 5),
    (30, N'Radomskie Taxi sp. z o.o.', N'Radom', '2025-10-06', N'taxi', 6)
) AS f (Nr, Nazwa, Miasto, DataRej, Kategoria, Aut)
JOIN dbo.Miejscowosci m ON m.Nazwa = f.Miasto;

-- KlientID nadawany w kolejnosci rejestracji
DROP TABLE IF EXISTS #MapK;
SELECT Tmp, ROW_NUMBER() OVER (ORDER BY DataRejestracji, Tmp) AS KlientID INTO #MapK FROM #K;

-- historia planow: plan poczatkowy, ewentualne przejscie na Plus i ewentualny powrot do Start
DROP TABLE IF EXISTS #H;
SELECT m.KlientID, h.PlanID, h.OdDaty
INTO #H
FROM #K k JOIN #MapK m ON m.Tmp = k.Tmp
CROSS APPLY (VALUES (k.PlanStart, k.DataRejestracji),
                    (CASE WHEN k.DataPlus IS NOT NULL THEN 2 END, k.DataPlus),
                    (CASE WHEN k.DataPowrotu IS NOT NULL THEN 1 END, k.DataPowrotu)) AS h (PlanID, OdDaty)
WHERE h.PlanID IS NOT NULL AND h.OdDaty <= '2025-12-31';

SET IDENTITY_INSERT dbo.Klienci ON;
INSERT INTO dbo.Klienci (KlientID, TypKlienta, Nazwa, MiejscowoscID, DataRejestracji, PlanID)
SELECT m.KlientID, k.TypKlienta, k.Nazwa, k.MiejscowoscID, k.DataRejestracji,
       (SELECT TOP (1) h.PlanID FROM #H h WHERE h.KlientID = m.KlientID ORDER BY h.OdDaty DESC)
FROM #K k JOIN #MapK m ON m.Tmp = k.Tmp
ORDER BY m.KlientID;
SET IDENTITY_INSERT dbo.Klienci OFF;

INSERT INTO dbo.HistoriaPlanow (KlientID, PlanID, OdDaty, DoDaty)
SELECT KlientID, PlanID, OdDaty, DATEADD(DAY, -1, LEAD(OdDaty) OVER (PARTITION BY KlientID ORDER BY OdDaty))
FROM #H;
GO
-- =====================================================================================
-- 3) Pojazdy klientow i ich "zachowanie" (czestotliwosc ladowania, stacja domowa)
-- =====================================================================================
DROP TABLE IF EXISTS #P;
WITH wagi AS (SELECT ModelPojazduID, Wi, Wf FROM (VALUES
    (1, 9, 8), (2, 12, 4), (3, 6, 8), (4, 5, 8), (5, 7, 10), (6, 5, 6), (7, 4, 3), (8, 3, 3), (9, 4, 8), (10, 3, 1), (11, 3, 1), (12, 3, 2),
    (13, 3, 1), (14, 2, 1), (15, 3, 2), (16, 3, 1), (17, 5, 3), (18, 3, 3), (19, 3, 1), (20, 3, 2), (21, 4, 1), (22, 2, 1), (23, 2, 1),
    (24, 1, 4), (25, 0, 0)) AS v (ModelPojazduID, Wi, Wf)),
cum AS (SELECT ModelPojazduID,
               (SUM(Wi) OVER (ORDER BY ModelPojazduID) - Wi) * 1.0 / SUM(Wi) OVER () AS IOd, SUM(Wi) OVER (ORDER BY ModelPojazduID) * 1.0 / SUM(Wi) OVER () AS IDo,
               (SUM(Wf) OVER (ORDER BY ModelPojazduID) - Wf) * 1.0 / SUM(Wf) OVER () AS FOd, SUM(Wf) OVER (ORDER BY ModelPojazduID) * 1.0 / SUM(Wf) OVER () AS FDo
        FROM wagi),
n AS (SELECT TOP (40) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS j FROM sys.all_objects),
auta AS (
    SELECT m.KlientID, k.Kategoria, k.MiejscowoscID, n.j, CAST(m.KlientID AS bigint) * 100 + n.j AS z,
           CASE WHEN n.j = 1 THEN k.DataRejestracji
                WHEN k.Kategoria = N'ind' THEN DATEADD(DAY, 90 + CAST(DATEDIFF(DAY, k.DataRejestracji, '2025-12-31') * gen.Los(m.KlientID, 120) AS int), k.DataRejestracji)
                ELSE DATEADD(DAY, CAST(DATEDIFF(DAY, k.DataRejestracji, '2025-12-31') * 0.85 * POWER(gen.Los(CAST(m.KlientID AS bigint) * 100 + n.j, 121), 2) AS int), k.DataRejestracji) END AS DataDodania
    FROM #K k JOIN #MapK m ON m.Tmp = k.Tmp JOIN n ON n.j <= k.LiczbaAut
)
SELECT ROW_NUMBER() OVER (ORDER BY a.DataDodania, a.KlientID, a.j) AS PojazdID, a.KlientID, a.Kategoria, a.MiejscowoscID, a.DataDodania,
       COALESCE(van.ModelPojazduID, mi.ModelPojazduID, mf.ModelPojazduID) AS ModelPojazduID,
       CAST(CASE WHEN 2019 + CAST(gen.Los(a.z, 124) * 7 AS int) > YEAR(a.DataDodania) THEN YEAR(a.DataDodania)
                 ELSE 2019 + CAST(gen.Los(a.z, 124) * 7 AS int) END AS smallint) AS RokProdukcji,
       CASE a.Kategoria WHEN N'taxi' THEN 3.5 + 2.0 * gen.Los(a.z, 125) WHEN N'kurier' THEN 2.5 + 1.5 * gen.Los(a.z, 125)
                        WHEN N'firma' THEN 1.0 + 1.0 * gen.Los(a.z, 125) ELSE 0.3 + 1.4 * POWER(gen.Los(a.z, 125), 2) END AS CzestTyg,
       CASE a.Kategoria WHEN N'taxi' THEN 0.7 WHEN N'kurier' THEN 0.6 WHEN N'firma' THEN 0.4 ELSE 0.15 + 0.3 * gen.Los(a.z, 126) END AS PreferujeSzybkie
INTO #P
FROM auta a
OUTER APPLY (SELECT 24 + CASE WHEN gen.Los(a.z, 123) < 0.5 THEN 1 ELSE 0 END AS ModelPojazduID WHERE a.Kategoria = N'kurier' AND gen.Los(a.z, 122) < 0.6) van
OUTER APPLY (SELECT c.ModelPojazduID FROM cum c WHERE a.Kategoria = N'ind' AND gen.Los(a.z, 122) >= c.IOd AND gen.Los(a.z, 122) < c.IDo) mi
OUTER APPLY (SELECT c.ModelPojazduID FROM cum c WHERE a.Kategoria <> N'ind' AND gen.Los(a.z, 127) >= c.FOd AND gen.Los(a.z, 127) < c.FDo) mf
WHERE a.DataDodania <= '2025-12-31';

SET IDENTITY_INSERT dbo.Pojazdy ON;
INSERT INTO dbo.Pojazdy (PojazdID, KlientID, ModelPojazduID, RokProdukcji, DataDodania)
SELECT PojazdID, KlientID, ModelPojazduID, RokProdukcji, DataDodania FROM #P ORDER BY PojazdID;
SET IDENTITY_INSERT dbo.Pojazdy OFF;

-- stacja "domowa": losowa stacja w miescie klienta, czynna najpozniej 90 dni po dodaniu pojazdu (gdy brak, najwczesniejsza)
CREATE TABLE gen.ProfilPojazdu (PojazdID int PRIMARY KEY, KlientID int, Kategoria nvarchar(10), DataDodania date, CzestTyg float,
                                PreferujeSzybkie float, StacjaDomowaID int, Bateria float, MaksAC float, MaksDC float);
INSERT INTO gen.ProfilPojazdu
SELECT p.PojazdID, p.KlientID, p.Kategoria, p.DataDodania, p.CzestTyg, p.PreferujeSzybkie, s.StacjaID,
       mp.PojemnoscBateriiKWh, mp.MaksMocACkW, mp.MaksMocDCkW
FROM #P p
JOIN dbo.ModelePojazdow mp ON mp.ModelPojazduID = p.ModelPojazduID
CROSS APPLY (SELECT TOP (1) s.StacjaID FROM dbo.Stacje s WHERE s.MiejscowoscID = p.MiejscowoscID
             ORDER BY CASE WHEN s.DataUruchomienia <= DATEADD(DAY, 90, p.DataDodania) THEN 0 ELSE 1 END,
                      CASE WHEN s.DataUruchomienia <= DATEADD(DAY, 90, p.DataDodania) THEN gen.Los(CAST(p.PojazdID AS bigint) * 100 + s.StacjaID, 130) ELSE 0 END,
                      s.DataUruchomienia) s;
GO
-- =====================================================================================
-- 4) Sesje klientow z kontem: dla kazdego pojazdu i dnia losowanie, czy tego dnia ladowal
-- =====================================================================================
DROP TABLE IF EXISTS #Kand;
SELECT ROW_NUMBER() OVER (ORDER BY d.Data, v.PojazdID) AS s, v.PojazdID, v.KlientID, v.Kategoria, v.StacjaDomowaID, v.PreferujeSzybkie,
       v.Bateria, v.MaksAC, v.MaksDC, d.DzienNr, d.Data, d.Miesiac, d.Wyjazdowy
INTO #Kand
FROM gen.ProfilPojazdu v
JOIN gen.Dni d ON d.Data >= v.DataDodania
JOIN dbo.HistoriaPlanow h ON h.KlientID = v.KlientID AND d.Data >= h.OdDaty AND d.Data <= ISNULL(h.DoDaty, '9999-12-31')
CROSS APPLY (SELECT
    CASE v.Kategoria WHEN N'kurier' THEN CASE WHEN d.DzienTyg <= 5 THEN 1.2 WHEN d.DzienTyg = 6 THEN 0.5 ELSE 0.1 END
                     WHEN N'firma'  THEN CASE WHEN d.DzienTyg <= 5 THEN 1.3 ELSE 0.2 END
                     WHEN N'taxi'   THEN 1.0
                     ELSE CASE WHEN d.DzienTyg IN (5, 6) THEN 1.2 WHEN d.DzienTyg = 7 THEN 1.0 ELSE 0.9 END END AS fTyg,
    CASE WHEN d.Miesiac IN (12, 1, 2) THEN 1.2 WHEN d.Miesiac IN (6, 7, 8) THEN 0.95 ELSE 1.0 END AS fSezon,
    CASE WHEN h.PlanID = 2 THEN 1.5 ELSE 1.0 END AS fPlan) f
WHERE gen.Los(CAST(v.PojazdID AS bigint) * 1000 + d.DzienNr, 11) < v.CzestTyg / 7.0 * f.fTyg * f.fSezon * f.fPlan;

DROP TABLE IF EXISTS #S;
CREATE TABLE #S (s bigint PRIMARY KEY, LadowarkaID int, StacjaID int, Typ nvarchar(20), Klasa varchar(3), MocKW float, Terminal bit,
                 KlientID int NULL, PojazdID int NULL, Kategoria nvarchar(10), Data date, Miesiac tinyint,
                 Bateria float, MaksAC float, MaksDC float, Start datetime2(0), Status nvarchar(30),
                 SocS float, SocK float, Energia float, Moc float, Postoj int, Metoda nvarchar(20),
                 KoniecLadowania datetime2(0) NULL, Odlaczenie datetime2(0) NULL);

INSERT INTO #S (s, LadowarkaID, StacjaID, Typ, Klasa, MocKW, Terminal, KlientID, PojazdID, Kategoria, Data, Miesiac, Bateria, MaksAC, MaksDC, Start)
SELECT k.s, l.LadowarkaID, st.StacjaID, st.TypLokalizacji, l.Klasa, l.MocKW, l.TerminalPlatniczy, k.KlientID, k.PojazdID, k.Kategoria,
       k.Data, k.Miesiac, k.Bateria, k.MaksAC, k.MaksDC,
       DATEADD(MINUTE, g.Godzina * 60 + CAST(gen.Los(k.s, 27) * 60 AS int), CAST(k.Data AS datetime2(0)))
FROM #Kand k
CROSS APPLY (SELECT CASE WHEN gen.Los(k.s, 21) < CASE WHEN k.Kategoria = N'ind' THEN k.Wyjazdowy ELSE 0.05 END THEN 1 ELSE 0 END AS Podroz) p
CROSS APPLY (SELECT TOP (1) s.StacjaID, s.TypLokalizacji FROM dbo.Stacje s
             WHERE (p.Podroz = 0 AND s.StacjaID = k.StacjaDomowaID)
                OR (p.Podroz = 1 AND s.StacjaID <> k.StacjaDomowaID AND s.DataUruchomienia <= k.Data)
             ORDER BY CASE WHEN p.Podroz = 1 AND s.TypLokalizacji = N'MOP' AND gen.Los(k.s, 22) < 0.7 THEN 0 ELSE 1 END,
                      gen.Los(k.s * 100 + s.StacjaID, 23)) st
CROSS APPLY (SELECT CASE WHEN p.Podroz = 1 OR st.TypLokalizacji = N'MOP' OR gen.Los(k.s, 24) < k.PreferujeSzybkie THEN 1 ELSE 0 END AS ChceSzybkie) c
CROSS APPLY (SELECT TOP (1) l.LadowarkaID, CAST(m.MocKW AS float) AS MocKW, m.TerminalPlatniczy,
                    CASE WHEN m.TypPradu = 'AC' THEN 'AC' WHEN m.MocKW >= 150 THEN 'HPC' ELSE 'DC' END AS Klasa
             FROM dbo.Ladowarki l JOIN dbo.ModeleLadowarek m ON m.ModelLadowarkiID = l.ModelLadowarkiID
             WHERE l.StacjaID = st.StacjaID AND l.DataInstalacji <= k.Data
             ORDER BY CASE WHEN (c.ChceSzybkie = 1 AND m.TypPradu = 'DC') OR (c.ChceSzybkie = 0 AND m.TypPradu = 'AC') THEN 0 ELSE 1 END,
                      gen.Los(k.s * 1000 + l.LadowarkaID, 25)) l
CROSS APPLY (SELECT TOP (1) pg.Godzina FROM gen.ProfilGodzin pg
             WHERE pg.Typ = st.TypLokalizacji AND gen.Los(k.s, 26) >= pg.CumOd AND gen.Los(k.s, 26) < pg.CumDo) g;
GO
-- =====================================================================================
-- 5) Sesje jednorazowe (bez konta, platnosc karta lub kodem QR): liczba na stacje i dzien
-- =====================================================================================
WITH lam AS (SELECT * FROM (VALUES (N'MOP', 2.5), (N'centrum handlowe', 1.2), (N'parking miejski', 1.0), (N'hotel', 0.5),
                                   (N'biurowiec', 0.3), (N'osiedle', 0.3)) AS v (Typ, L)),
dzienne AS (
    SELECT s.StacjaID, s.TypLokalizacji, d.DzienNr, d.Data, d.Miesiac,
           l.L * (0.7 + 0.6 * d.DzienNr / 731.0) * CASE WHEN s.TypLokalizacji = N'MOP' THEN 1 + 5 * (d.Wyjazdowy - 0.1) ELSE 1 END AS Lambda
    FROM dbo.Stacje s JOIN lam l ON l.Typ = s.TypLokalizacji JOIN gen.Dni d ON d.Data >= s.DataUruchomienia
),
ile AS (
    SELECT *, CAST(FLOOR(Lambda) AS int) + CASE WHEN gen.Los(CAST(StacjaID AS bigint) * 1000 + DzienNr, 41) < Lambda - FLOOR(Lambda) THEN 1 ELSE 0 END AS N
    FROM dzienne
),
sesje AS (
    SELECT 100000000 + ROW_NUMBER() OVER (ORDER BY i.Data, i.StacjaID, n.j) AS s, i.StacjaID, i.TypLokalizacji, i.Data, i.Miesiac
    FROM ile i JOIN (SELECT TOP (20) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS j FROM sys.all_objects) n ON n.j <= i.N
)
INSERT INTO #S (s, LadowarkaID, StacjaID, Typ, Klasa, MocKW, Terminal, KlientID, PojazdID, Kategoria, Data, Miesiac, Bateria, MaksAC, MaksDC, Start)
SELECT x.s, l.LadowarkaID, x.StacjaID, x.TypLokalizacji, l.Klasa, l.MocKW, l.TerminalPlatniczy, NULL, NULL, N'gosc', x.Data, x.Miesiac,
       40 + 40 * gen.Los(x.s, 42), 11, 50 + 150 * gen.Los(x.s, 43),
       DATEADD(MINUTE, g.Godzina * 60 + CAST(gen.Los(x.s, 27) * 60 AS int), CAST(x.Data AS datetime2(0)))
FROM sesje x
CROSS APPLY (SELECT CASE WHEN x.TypLokalizacji = N'MOP' OR gen.Los(x.s, 24) < 0.5 THEN 1 ELSE 0 END AS ChceSzybkie) c
CROSS APPLY (SELECT TOP (1) l.LadowarkaID, CAST(m.MocKW AS float) AS MocKW, m.TerminalPlatniczy,
                    CASE WHEN m.TypPradu = 'AC' THEN 'AC' WHEN m.MocKW >= 150 THEN 'HPC' ELSE 'DC' END AS Klasa
             FROM dbo.Ladowarki l JOIN dbo.ModeleLadowarek m ON m.ModelLadowarkiID = l.ModelLadowarkiID
             WHERE l.StacjaID = x.StacjaID AND l.DataInstalacji <= x.Data
             ORDER BY CASE WHEN (c.ChceSzybkie = 1 AND m.TypPradu = 'DC') OR (c.ChceSzybkie = 0 AND m.TypPradu = 'AC') THEN 0 ELSE 1 END,
                      gen.Los(x.s * 1000 + l.LadowarkaID, 25)) l
CROSS APPLY (SELECT TOP (1) pg.Godzina FROM gen.ProfilGodzin pg
             WHERE pg.Typ = x.TypLokalizacji AND gen.Los(x.s, 26) >= pg.CumOd AND gen.Los(x.s, 26) < pg.CumDo) g;
GO
-- =====================================================================================
-- 6) Przebieg sesji: stan baterii, energia, moc, czas ladowania i postoju, metoda platnosci
-- =====================================================================================
UPDATE #S SET
    SocS = CASE WHEN Klasa = 'AC' THEN 20 + 40 * gen.Los(s, 51) ELSE 8 + 32 * gen.Los(s, 51) END - CASE WHEN Miesiac IN (12, 1, 2) THEN 5 ELSE 0 END,
    SocK = CASE WHEN Klasa = 'AC' THEN 80 + 20 * gen.Los(s, 52) ELSE 70 + 20 * gen.Los(s, 52) END,
    Status = CASE WHEN gen.Los(s, 55) < 0.02 THEN N'przerwana przez użytkownika' ELSE N'zakończona' END,
    Moc = CASE WHEN Klasa = 'AC' THEN CASE WHEN MocKW < MaksAC THEN MocKW ELSE MaksAC END * (0.85 + 0.10 * gen.Los(s, 53))
               ELSE CASE WHEN MocKW < MaksDC THEN MocKW ELSE MaksDC END * (0.50 + 0.25 * gen.Los(s, 53))
                    * CASE WHEN Miesiac IN (12, 1, 2) THEN 0.8 ELSE 1.0 END END,
    Metoda = CASE WHEN KlientID IS NULL THEN CASE WHEN Terminal = 1 AND gen.Los(s, 56) < 0.65 THEN N'terminal płatniczy' ELSE N'kod QR' END
                  WHEN Kategoria = N'ind' THEN CASE WHEN gen.Los(s, 56) < 0.85 THEN N'aplikacja' ELSE N'karta RFID' END
                  ELSE CASE WHEN gen.Los(s, 56) < 0.90 THEN N'karta RFID' ELSE N'aplikacja' END END;

UPDATE #S SET SocK = SocS + (SocK - SocS) * (0.2 + 0.4 * gen.Los(s, 57)) WHERE Status = N'przerwana przez użytkownika';
UPDATE #S SET Energia = (SocK - SocS) / 100.0 * Bateria;

-- czas postoju po zakonczeniu ladowania: AC na osiedlu i w hotelu wieczorem czesto do rana
UPDATE #S SET Postoj = CAST(
    CASE WHEN Klasa = 'AC' AND Typ IN (N'osiedle', N'hotel') AND DATEPART(HOUR, Start) >= 17 AND gen.Los(s, 58) < 0.6
              THEN DATEDIFF(MINUTE, DATEADD(MINUTE, Energia / Moc * 60 + 2, Start), DATEADD(HOUR, 30, CAST(Data AS datetime2(0)))) + 180 * gen.Los(s, 54)
         WHEN Klasa = 'AC' THEN 120 * POWER(gen.Los(s, 54), 2)
         ELSE (CASE WHEN gen.Los(s, 54) < 0.7 THEN gen.Los(s, 54) / 0.7 * 8
                    WHEN gen.Los(s, 54) < 0.9 THEN 8 + (gen.Los(s, 54) - 0.7) / 0.2 * 17
                    ELSE 25 + (gen.Los(s, 54) - 0.9) / 0.1 * 45 END)
              * CASE WHEN Typ IN (N'MOP', N'centrum handlowe') THEN 1.3 ELSE 1.0 END END AS int);
UPDATE #S SET Postoj = 0 WHERE Postoj < 0;

UPDATE #S SET KoniecLadowania = DATEADD(MINUTE, CAST(CEILING(Energia / Moc * 60) AS int) + 2, Start);
UPDATE #S SET Odlaczenie = DATEADD(MINUTE, Postoj, KoniecLadowania);
GO
-- jedna ladowarka obsluguje jeden samochod naraz: usuwane sa sesje, ktore zaczynaja sie przed odlaczeniem poprzedniego auta
DROP TABLE IF EXISTS #Kolizje;
SELECT s INTO #Kolizje FROM (
    SELECT s, Start, MAX(Odlaczenie) OVER (PARTITION BY LadowarkaID ORDER BY Start, s ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) AS Zajeta
    FROM #S) x
WHERE Start < Zajeta;
DELETE FROM #S WHERE s IN (SELECT s FROM #Kolizje);
GO
-- =====================================================================================
-- 7) Awarie ladowarek: czestosc zalezy od modelu, czas naprawy od kategorii
-- =====================================================================================
DROP TABLE IF EXISTS #A;
WITH czestosc AS (SELECT * FROM (VALUES (1, 1.0), (2, 2.5), (3, 4.0), (4, 1.5), (5, 2.0), (6, 3.0)) AS v (ModelLadowarkiID, NaRok)),
kat AS (SELECT TypPradu, KategoriaID, Hmin, Hmax,
               (SUM(W) OVER (PARTITION BY TypPradu ORDER BY KategoriaID) - W) / SUM(W) OVER (PARTITION BY TypPradu) AS CumOd,
               SUM(W) OVER (PARTITION BY TypPradu ORDER BY KategoriaID) / SUM(W) OVER (PARTITION BY TypPradu) AS CumDo
        FROM (VALUES ('AC', 1, 35.0, 1, 8), ('AC', 2, 30, 12, 72), ('AC', 3, 5, 24, 144), ('AC', 5, 20, 24, 168), ('AC', 6, 10, 1, 6),
                     ('DC', 1, 25, 1, 8), ('DC', 2, 20, 12, 72), ('DC', 3, 25, 24, 144), ('DC', 4, 15, 2, 24), ('DC', 5, 5, 24, 168), ('DC', 6, 10, 1, 6)
             ) AS v (TypPradu, KategoriaID, W, Hmin, Hmax)),
zdarzenia AS (
    SELECT l.LadowarkaID, m.TypPradu, d.Data, CAST(l.LadowarkaID AS bigint) * 1000 + d.DzienNr AS z
    FROM dbo.Ladowarki l JOIN dbo.ModeleLadowarek m ON m.ModelLadowarkiID = l.ModelLadowarkiID
    JOIN czestosc c ON c.ModelLadowarkiID = l.ModelLadowarkiID
    JOIN gen.Dni d ON d.Data >= l.DataInstalacji
    WHERE gen.Los(CAST(l.LadowarkaID AS bigint) * 1000 + d.DzienNr, 31) < c.NaRok / 365.0
)
SELECT z.LadowarkaID, k.KategoriaID, x.Poczatek,
       DATEADD(MINUTE, CAST((k.Hmin + (k.Hmax - k.Hmin) * POWER(gen.Los(z.z, 34), 2)) * 60 AS int), x.Poczatek) AS Koniec
INTO #A
FROM zdarzenia z
JOIN kat k ON k.TypPradu = z.TypPradu AND gen.Los(z.z, 32) >= k.CumOd AND gen.Los(z.z, 32) < k.CumDo
CROSS APPLY (SELECT DATEADD(MINUTE, CAST(gen.Los(z.z, 33) * 1440 AS int), CAST(z.Data AS datetime2(0))) AS Poczatek) x;

-- awaria w trakcie poprzedniej awarii tej samej ladowarki nie jest osobnym zdarzeniem
WITH x AS (SELECT *, MAX(Koniec) OVER (PARTITION BY LadowarkaID ORDER BY Poczatek ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) AS Trwa FROM #A)
DELETE FROM x WHERE Poczatek < Trwa;

-- ladowarka w awarii nie przyjmuje nowych sesji
DELETE s FROM #S s WHERE EXISTS (SELECT 1 FROM #A a WHERE a.LadowarkaID = s.LadowarkaID AND s.Start >= a.Poczatek AND s.Start < a.Koniec);

-- sesja trwajaca w chwili awarii zostaje przerwana
UPDATE s SET
    Energia = s.Energia * DATEDIFF(SECOND, s.Start, a.Poczatek) / NULLIF(DATEDIFF(SECOND, s.Start, s.KoniecLadowania), 0),
    SocK = s.SocS + (s.SocK - s.SocS) * DATEDIFF(SECOND, s.Start, a.Poczatek) / NULLIF(DATEDIFF(SECOND, s.Start, s.KoniecLadowania), 0),
    KoniecLadowania = a.Poczatek,
    Odlaczenie = DATEADD(MINUTE, 3 + CAST(gen.Los(s.s, 59) * 8 AS int), a.Poczatek),
    Postoj = 0,
    Status = N'przerwana (awaria)'
FROM #S s JOIN #A a ON a.LadowarkaID = s.LadowarkaID AND a.Poczatek > s.Start AND a.Poczatek < s.KoniecLadowania;
GO
-- =====================================================================================
-- 8) Zapis do tabel bazy: ceny z cennika, rabat z planu obowiazujacego w dniu sesji
-- =====================================================================================
INSERT INTO dbo.Sesje (LadowarkaID, KlientID, PojazdID, MetodaPlatnosci, Start, KoniecLadowania, Odlaczenie, SocStart, SocKoniec,
                       EnergiaKWh, CenaZaKWh, KwotaEnergia, KwotaRabatu, KwotaPostoj, KwotaRazem, Status)
SELECT s.LadowarkaID, s.KlientID, s.PojazdID, s.Metoda, s.Start, s.KoniecLadowania, s.Odlaczenie,
       CASE WHEN s.Klasa <> 'AC' THEN CAST(ROUND(s.SocS, 0) AS tinyint) END,
       CASE WHEN s.Klasa <> 'AC' THEN CAST(ROUND(s.SocK, 0) AS tinyint) END,
       e.Energia, c.CenaZaKWh, k.KwotaEnergia, r.KwotaRabatu, p.KwotaPostoj,
       k.KwotaEnergia - r.KwotaRabatu + p.KwotaPostoj, s.Status
FROM #S s
JOIN dbo.Cennik c ON c.KlasaMocy = s.Klasa AND s.Data >= c.ObowiazujeOd AND s.Data <= ISNULL(c.ObowiazujeDo, '9999-12-31')
LEFT JOIN dbo.HistoriaPlanow h ON h.KlientID = s.KlientID AND s.Data >= h.OdDaty AND s.Data <= ISNULL(h.DoDaty, '9999-12-31')
LEFT JOIN dbo.PlanyTaryfowe pt ON pt.PlanID = h.PlanID
CROSS APPLY (SELECT CAST(ROUND(s.Energia, 3) AS decimal(7,3)) AS Energia) e
CROSS APPLY (SELECT CAST(ROUND(e.Energia * c.CenaZaKWh, 2) AS money) AS KwotaEnergia) k
CROSS APPLY (SELECT CAST(ROUND(k.KwotaEnergia * ISNULL(pt.RabatProcent, 0) / 100, 2) AS money) AS KwotaRabatu) r
CROSS APPLY (SELECT CAST(CASE WHEN s.Postoj > c.DarmowyPostojMin THEN (s.Postoj - c.DarmowyPostojMin) * c.OplataPostojowaZaMin ELSE 0 END AS money) AS KwotaPostoj) p
ORDER BY s.Start, s.s;

INSERT INTO dbo.Awarie (LadowarkaID, KategoriaID, Poczatek, Koniec)
SELECT LadowarkaID, KategoriaID, Poczatek, Koniec FROM #A ORDER BY Poczatek, LadowarkaID;
GO
-- sprzatanie obiektow pomocniczych generatora
DROP TABLE IF EXISTS gen.Dni, gen.ProfilGodzin, gen.WagiMiast, gen.ProfilPojazdu;
DROP FUNCTION IF EXISTS gen.Los;
DROP SCHEMA IF EXISTS gen;
GO
SELECT N'Klienci' AS Tabela, COUNT(*) AS Wierszy FROM dbo.Klienci
UNION ALL SELECT N'HistoriaPlanow', COUNT(*) FROM dbo.HistoriaPlanow
UNION ALL SELECT N'Pojazdy', COUNT(*) FROM dbo.Pojazdy
UNION ALL SELECT N'Sesje', COUNT(*) FROM dbo.Sesje
UNION ALL SELECT N'Awarie', COUNT(*) FROM dbo.Awarie;
GO
