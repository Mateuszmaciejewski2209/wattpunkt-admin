-- WattPunkt Admin: baza danych aplikacji do obslugi sieci stacji ladowania samochodow elektrycznych firmy WattPunkt sp. z o.o.
-- Serwer: SQL Server 2019 lub nowszy (wystarczy Express). Skrypt tworzy baze, wszystkie tabele z ograniczeniami i dane slownikowe.
-- Dane (klienci, pojazdy, sesje ladowania, awarie) tworzy skrypt 02_generuj_dane.sql, a konta testowe pracownikow 03_konta_testowe.sql.
USE master;
GO
IF DB_ID(N'WattPunktAdmin') IS NOT NULL
BEGIN
    ALTER DATABASE WattPunktAdmin SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE WattPunktAdmin;
END
GO
CREATE DATABASE WattPunktAdmin;
GO
USE WattPunktAdmin;
GO
-- =====================================================================================
-- TABELE
-- =====================================================================================
CREATE TABLE dbo.Miejscowosci (
    MiejscowoscID   int IDENTITY(1,1) PRIMARY KEY,
    Nazwa           nvarchar(40) NOT NULL,
    Wojewodztwo     nvarchar(30) NOT NULL,
    CONSTRAINT UQ_Miejscowosci UNIQUE (Nazwa, Wojewodztwo)
);
CREATE TABLE dbo.Stacje (
    StacjaID          int IDENTITY(1,1) PRIMARY KEY,
    Kod               varchar(10)  NOT NULL UNIQUE,
    Nazwa             nvarchar(60) NOT NULL,
    MiejscowoscID     int NOT NULL REFERENCES dbo.Miejscowosci (MiejscowoscID),
    TypLokalizacji    nvarchar(20) NOT NULL
        CHECK (TypLokalizacji IN (N'osiedle', N'biurowiec', N'centrum handlowe', N'parking miejski', N'hotel', N'MOP')),
    DataUruchomienia  date NOT NULL,
    Wersja            rowversion,                                               -- wersja wiersza: wykrywanie rownoczesnej edycji
    CONSTRAINT CK_Stacje_Kod CHECK (LEN(Kod) = 5 AND Kod NOT LIKE '%[^A-Z0-9]%')  -- kod: 5 wielkich liter lub cyfr, np. WAW07
);
CREATE TABLE dbo.ModeleLadowarek (
    ModelLadowarkiID  int IDENTITY(1,1) PRIMARY KEY,
    Producent         nvarchar(30) NOT NULL,
    Model             nvarchar(30) NOT NULL,
    MocKW             smallint     NOT NULL,
    TypPradu          char(2)      NOT NULL CHECK (TypPradu IN ('AC', 'DC')),
    Zlacza            nvarchar(30) NOT NULL,
    TerminalPlatniczy bit          NOT NULL
);
CREATE TABLE dbo.Ladowarki (
    LadowarkaID       int IDENTITY(1,1) PRIMARY KEY,
    StacjaID          int NOT NULL REFERENCES dbo.Stacje (StacjaID),
    ModelLadowarkiID  int NOT NULL REFERENCES dbo.ModeleLadowarek (ModelLadowarkiID),
    NumerSeryjny      varchar(20) NOT NULL UNIQUE,
    DataInstalacji    date NOT NULL,
    Wersja            rowversion
);
CREATE TABLE dbo.ModelePojazdow (
    ModelPojazduID      int IDENTITY(1,1) PRIMARY KEY,
    Marka               nvarchar(20) NOT NULL,
    Model               nvarchar(30) NOT NULL,
    Segment             nvarchar(20) NOT NULL,
    PojemnoscBateriiKWh decimal(5,1) NOT NULL,
    MaksMocACkW         decimal(4,1) NOT NULL,
    MaksMocDCkW         smallint     NOT NULL
);
CREATE TABLE dbo.PlanyTaryfowe (
    PlanID            int PRIMARY KEY,
    Nazwa             nvarchar(20)  NOT NULL UNIQUE,
    OplataMiesieczna  money         NOT NULL,
    RabatProcent      decimal(4,1)  NOT NULL,
    Opis              nvarchar(200) NULL
);
CREATE TABLE dbo.Klienci (
    KlientID          int IDENTITY(1,1) PRIMARY KEY,
    TypKlienta        nvarchar(15) NOT NULL CHECK (TypKlienta IN (N'indywidualny', N'firmowy')),
    Nazwa             nvarchar(80) NOT NULL,
    MiejscowoscID     int  NOT NULL REFERENCES dbo.Miejscowosci (MiejscowoscID),
    DataRejestracji   date NOT NULL,
    PlanID            int  NOT NULL REFERENCES dbo.PlanyTaryfowe (PlanID),     -- plan obowiazujacy obecnie
    Wersja            rowversion
);
-- historia planow klienta: kazda zmiana planu to nowy wiersz z okresem obowiazywania
CREATE TABLE dbo.HistoriaPlanow (
    KlientID  int  NOT NULL REFERENCES dbo.Klienci (KlientID),
    PlanID    int  NOT NULL REFERENCES dbo.PlanyTaryfowe (PlanID),
    OdDaty    date NOT NULL,
    DoDaty    date NULL,                                                         -- NULL = plan obowiazuje obecnie
    CONSTRAINT PK_HistoriaPlanow PRIMARY KEY (KlientID, OdDaty)
);
CREATE TABLE dbo.Pojazdy (
    PojazdID        int IDENTITY(1,1) PRIMARY KEY,
    KlientID        int      NOT NULL REFERENCES dbo.Klienci (KlientID),
    ModelPojazduID  int      NOT NULL REFERENCES dbo.ModelePojazdow (ModelPojazduID),
    RokProdukcji    smallint NOT NULL CONSTRAINT CK_Pojazdy_Rok CHECK (RokProdukcji BETWEEN 2000 AND 2100),
    DataDodania     date     NOT NULL,
    Wersja          rowversion
);
CREATE TABLE dbo.Cennik (
    CennikID              int IDENTITY(1,1) PRIMARY KEY,
    KlasaMocy             varchar(3) NOT NULL CHECK (KlasaMocy IN ('AC', 'DC', 'HPC')),
    ObowiazujeOd          date       NOT NULL,
    ObowiazujeDo          date       NULL,
    CenaZaKWh             money      NOT NULL,
    OplataPostojowaZaMin  money      NOT NULL,                                  -- oplata za blokowanie ladowarki po zakonczeniu ladowania
    DarmowyPostojMin      smallint   NOT NULL
);
CREATE TABLE dbo.Sesje (
    SesjaID          bigint IDENTITY(1,1) PRIMARY KEY,
    LadowarkaID      int NOT NULL REFERENCES dbo.Ladowarki (LadowarkaID),
    KlientID         int NULL REFERENCES dbo.Klienci (KlientID),                -- NULL = platnosc jednorazowa bez konta
    PojazdID         int NULL REFERENCES dbo.Pojazdy (PojazdID),
    MetodaPlatnosci  nvarchar(20) NOT NULL,
    Start            datetime2(0) NOT NULL,
    KoniecLadowania  datetime2(0) NOT NULL,
    Odlaczenie       datetime2(0) NOT NULL,                                      -- odlaczenie kabla, po nim ladowarka jest wolna
    SocStart         tinyint NULL,                                               -- stan baterii w %, ladowarki AC go nie odczytuja
    SocKoniec        tinyint NULL,
    EnergiaKWh       decimal(7,3) NOT NULL,
    CenaZaKWh        money NOT NULL,
    KwotaEnergia     money NOT NULL,
    KwotaRabatu      money NOT NULL,
    KwotaPostoj      money NOT NULL,
    KwotaRazem       money NOT NULL,
    Status           nvarchar(30) NOT NULL
);
CREATE INDEX IX_Sesje_Ladowarka ON dbo.Sesje (LadowarkaID, Start);
CREATE INDEX IX_Sesje_Klient ON dbo.Sesje (KlientID, Start);
CREATE INDEX IX_Sesje_Start ON dbo.Sesje (Start DESC);                         -- przegladanie sesji od najnowszych
CREATE TABLE dbo.KategorieAwarii (
    KategoriaID  int PRIMARY KEY,
    Nazwa        nvarchar(40) NOT NULL,
    Grupa        nvarchar(20) NOT NULL
);
CREATE TABLE dbo.Awarie (
    AwariaID     int IDENTITY(1,1) PRIMARY KEY,
    LadowarkaID  int NOT NULL REFERENCES dbo.Ladowarki (LadowarkaID),
    KategoriaID  int NOT NULL REFERENCES dbo.KategorieAwarii (KategoriaID),
    Poczatek     datetime2(0) NOT NULL,
    Koniec       datetime2(0) NULL,                                              -- NULL = awaria jeszcze trwa
    Opis         nvarchar(200) NULL,
    Wersja       rowversion,
    CONSTRAINT CK_Awarie_Koniec CHECK (Koniec IS NULL OR Koniec >= Poczatek)
);

-- konta pracownikow: hasla tylko jako skrot PBKDF2 (SHA-256, 100 000 iteracji) z losowa sola
CREATE TABLE dbo.Pracownicy (
    PracownikID        int IDENTITY(1,1) PRIMARY KEY,
    Login              varchar(30)   NOT NULL CONSTRAINT UQ_Pracownicy_Login UNIQUE,
    Imie               nvarchar(40)  NOT NULL,
    Nazwisko           nvarchar(40)  NOT NULL,
    Rola               nvarchar(20)  NOT NULL CONSTRAINT CK_Pracownicy_Rola CHECK (Rola IN (N'Pracownik', N'Administrator')),
    HasloHash          varbinary(32) NOT NULL,
    HasloSol           varbinary(16) NOT NULL,
    Iteracje           int           NOT NULL,
    Aktywny            bit           NOT NULL CONSTRAINT DF_Pracownicy_Aktywny DEFAULT 1,
    NieudaneProby      int           NOT NULL CONSTRAINT DF_Pracownicy_Proby DEFAULT 0,   -- 5 = konto zablokowane
    WymagaZmianyHasla  bit           NOT NULL CONSTRAINT DF_Pracownicy_Zmiana DEFAULT 0,
    DataUtworzenia     datetime2(0)  NOT NULL CONSTRAINT DF_Pracownicy_Data DEFAULT SYSDATETIME(),
    OstatnieLogowanie  datetime2(0)  NULL,
    Wersja             rowversion
);

-- dziennik zmian: kto, kiedy i co zmienil
CREATE TABLE dbo.DziennikZmian (
    WpisID       bigint IDENTITY(1,1) PRIMARY KEY,
    Data         datetime2(0)  NOT NULL CONSTRAINT DF_DziennikZmian_Data DEFAULT SYSDATETIME(),
    PracownikID  int           NOT NULL REFERENCES dbo.Pracownicy (PracownikID),
    Operacja     nvarchar(20)  NOT NULL CONSTRAINT CK_DziennikZmian_Operacja
                     CHECK (Operacja IN (N'dodanie', N'edycja', N'usunięcie', N'logowanie', N'konto')),
    Obiekt       nvarchar(30)  NOT NULL,
    ObiektID     int           NULL,
    Opis         nvarchar(400) NOT NULL
);
CREATE INDEX IX_DziennikZmian_Data ON dbo.DziennikZmian (Data DESC);
GO
-- =====================================================================================
-- DANE SLOWNIKOWE
-- =====================================================================================
INSERT INTO dbo.Miejscowosci (Nazwa, Wojewodztwo) VALUES
 (N'Warszawa', N'mazowieckie'), (N'Kraków', N'małopolskie'), (N'Wrocław', N'dolnośląskie'), (N'Łódź', N'łódzkie'),
 (N'Poznań', N'wielkopolskie'), (N'Gdańsk', N'pomorskie'), (N'Gdynia', N'pomorskie'), (N'Szczecin', N'zachodniopomorskie'),
 (N'Bydgoszcz', N'kujawsko-pomorskie'), (N'Toruń', N'kujawsko-pomorskie'), (N'Lublin', N'lubelskie'), (N'Białystok', N'podlaskie'),
 (N'Katowice', N'śląskie'), (N'Gliwice', N'śląskie'), (N'Rzeszów', N'podkarpackie'), (N'Olsztyn', N'warmińsko-mazurskie'),
 (N'Kielce', N'świętokrzyskie'), (N'Opole', N'opolskie'), (N'Zielona Góra', N'lubuskie'), (N'Radom', N'mazowieckie'),
 -- miejscowosci przy autostradach i drogach ekspresowych (stacje MOP)
 (N'Stryków', N'łódzkie'), (N'Września', N'wielkopolskie'), (N'Wiskitki', N'mazowieckie'), (N'Piotrków Trybunalski', N'łódzkie'),
 (N'Kowal', N'kujawsko-pomorskie'), (N'Częstochowa', N'śląskie'), (N'Brzeg', N'opolskie'), (N'Legnica', N'dolnośląskie'),
 (N'Tarnów', N'małopolskie'), (N'Grójec', N'mazowieckie'), (N'Jędrzejów', N'świętokrzyskie'), (N'Ostrów Mazowiecka', N'mazowieckie'),
 (N'Elbląg', N'warmińsko-mazurskie');

INSERT INTO dbo.Stacje (Kod, Nazwa, MiejscowoscID, TypLokalizacji, DataUruchomienia)
SELECT s.Kod, s.Nazwa, m.MiejscowoscID, s.Typ, s.Otwarcie
FROM (VALUES
 ('WAW01', N'Warszawa Ursynów',           N'Warszawa',             N'osiedle',          '2022-05-10'),
 ('WAW02', N'Warszawa Wola',              N'Warszawa',             N'biurowiec',        '2022-09-01'),
 ('WAW03', N'Warszawa Targówek',          N'Warszawa',             N'centrum handlowe', '2023-03-15'),
 ('WAW04', N'Warszawa Śródmieście',       N'Warszawa',             N'parking miejski',  '2023-06-01'),
 ('WAW05', N'Warszawa Białołęka',         N'Warszawa',             N'osiedle',          '2024-09-02'),
 ('WAW06', N'Warszawa Mokotów',           N'Warszawa',             N'hotel',            '2025-02-10'),
 ('KRK01', N'Kraków Ruczaj',              N'Kraków',               N'osiedle',          '2022-11-21'),
 ('KRK02', N'Kraków Zabłocie',            N'Kraków',               N'biurowiec',        '2023-04-12'),
 ('KRK03', N'Kraków Czyżyny',             N'Kraków',               N'centrum handlowe', '2024-03-01'),
 ('WRO01', N'Wrocław Krzyki',             N'Wrocław',              N'centrum handlowe', '2022-10-05'),
 ('WRO02', N'Wrocław Nadodrze',           N'Wrocław',              N'parking miejski',  '2023-07-20'),
 ('WRO03', N'Wrocław Jagodno',            N'Wrocław',              N'osiedle',          '2025-04-15'),
 ('LOD01', N'Łódź Widzew',                N'Łódź',                 N'centrum handlowe', '2023-01-25'),
 ('LOD02', N'Łódź Śródmieście',           N'Łódź',                 N'hotel',            '2023-09-14'),
 ('POZ01', N'Poznań Rataje',              N'Poznań',               N'osiedle',          '2022-08-08'),
 ('POZ02', N'Poznań Grunwald',            N'Poznań',               N'biurowiec',        '2023-05-30'),
 ('POZ03', N'Poznań Franowo',             N'Poznań',               N'centrum handlowe', '2024-11-04'),
 ('GDA01', N'Gdańsk Oliwa',               N'Gdańsk',               N'biurowiec',        '2023-02-20'),
 ('GDA02', N'Gdańsk Śródmieście',         N'Gdańsk',               N'hotel',            '2024-05-06'),
 ('GDY01', N'Gdynia Redłowo',             N'Gdynia',               N'centrum handlowe', '2023-08-10'),
 ('SZZ01', N'Szczecin Pogodno',           N'Szczecin',             N'osiedle',          '2023-10-02'),
 ('SZZ02', N'Szczecin Centrum',           N'Szczecin',             N'parking miejski',  '2025-06-16'),
 ('BYD01', N'Bydgoszcz Fordon',           N'Bydgoszcz',            N'centrum handlowe', '2024-01-22'),
 ('TOR01', N'Toruń Bielany',              N'Toruń',                N'parking miejski',  '2024-06-24'),
 ('LUB01', N'Lublin Czechów',             N'Lublin',               N'centrum handlowe', '2023-11-13'),
 ('BIA01', N'Białystok Centrum',          N'Białystok',            N'parking miejski',  '2024-04-08'),
 ('KTW01', N'Katowice Brynów',            N'Katowice',             N'biurowiec',        '2022-12-12'),
 ('KTW02', N'Katowice Centrum',           N'Katowice',             N'hotel',            '2024-08-19'),
 ('GLI01', N'Gliwice Sośnica',            N'Gliwice',              N'centrum handlowe', '2025-03-03'),
 ('RZE01', N'Rzeszów Słocina',            N'Rzeszów',              N'centrum handlowe', '2024-10-14'),
 ('OLS01', N'Olsztyn Jaroty',             N'Olsztyn',              N'osiedle',          '2025-01-20'),
 ('KIE01', N'Kielce Centrum',             N'Kielce',               N'parking miejski',  '2024-07-01'),
 ('OPO01', N'Opole Zaodrze',              N'Opole',                N'osiedle',          '2025-05-12'),
 ('ZGO01', N'Zielona Góra Centrum',       N'Zielona Góra',         N'parking miejski',  '2025-08-04'),
 ('RAD01', N'Radom Centrum',              N'Radom',                N'parking miejski',  '2025-09-15'),
 ('A2STR', N'MOP Stryków (A2)',           N'Stryków',              N'MOP',              '2022-07-01'),
 ('A2WRZ', N'MOP Września (A2)',          N'Września',             N'MOP',              '2023-06-15'),
 ('A2WIS', N'MOP Wiskitki (A2)',          N'Wiskitki',             N'MOP',              '2024-02-12'),
 ('A1PIO', N'MOP Piotrków (A1)',          N'Piotrków Trybunalski', N'MOP',              '2022-11-02'),
 ('A1KOW', N'MOP Kowal (A1)',             N'Kowal',                N'MOP',              '2023-09-25'),
 ('A1CZE', N'MOP Częstochowa (A1)',       N'Częstochowa',          N'MOP',              '2024-12-09'),
 ('A4BRZ', N'MOP Brzeg (A4)',             N'Brzeg',                N'MOP',              '2023-04-03'),
 ('A4LEG', N'MOP Legnica (A4)',           N'Legnica',              N'MOP',              '2024-06-03'),
 ('A4TAR', N'MOP Tarnów (A4)',            N'Tarnów',               N'MOP',              '2025-03-24'),
 ('S7GRO', N'MOP Grójec (S7)',            N'Grójec',               N'MOP',              '2023-12-04'),
 ('S7JED', N'MOP Jędrzejów (S7)',         N'Jędrzejów',            N'MOP',              '2025-07-07'),
 ('S7ELB', N'MOP Elbląg (S7)',            N'Elbląg',               N'MOP',              '2025-10-06'),
 ('S8OST', N'MOP Ostrów Mazowiecka (S8)', N'Ostrów Mazowiecka',    N'MOP',              '2024-09-30')
) AS s (Kod, Nazwa, Miejscowosc, Typ, Otwarcie)
JOIN dbo.Miejscowosci m ON m.Nazwa = s.Miejscowosc
ORDER BY s.Otwarcie, s.Kod;

-- producenci ladowarek sa fikcyjni
INSERT INTO dbo.ModeleLadowarek (Producent, Model, MocKW, TypPradu, Zlacza, TerminalPlatniczy) VALUES
 (N'Voltaris',  N'AC Duo 22',       22, 'AC', N'Typ 2',         0),
 (N'Amperis',   N'CityCharge 22',   22, 'AC', N'Typ 2',         0),
 (N'Elektrion', N'DC Compact 50',   50, 'DC', N'CCS2, CHAdeMO', 1),
 (N'Voltaris',  N'Rapid 60',        60, 'DC', N'CCS2',          1),
 (N'Nordwatt',  N'HPC 150',        150, 'DC', N'CCS2',          1),
 (N'Elektrion', N'Ultra 300',      300, 'DC', N'CCS2',          1);

-- ladowarki: liczba i rodzaj zalezy od typu lokalizacji; do konca 2023 kupowano AC Amperis i DC Elektrion, potem Voltaris
WITH konf AS (
    SELECT * FROM (VALUES
     (N'osiedle', 'AC', 3), (N'biurowiec', 'AC', 4), (N'biurowiec', 'DC', 1), (N'centrum handlowe', 'AC', 2), (N'centrum handlowe', 'DC', 2),
     (N'parking miejski', 'AC', 2), (N'parking miejski', 'DC', 1), (N'hotel', 'AC', 2), (N'MOP', 'DC', 2), (N'MOP', 'HPC', 2)
    ) AS k (Typ, Klasa, Ile)
), n AS (SELECT * FROM (VALUES (1), (2), (3), (4)) AS v (i)),
nowe AS (
    SELECT s.StacjaID, s.Kod, s.DataUruchomienia AS DataInstalacji, n.i,
           CASE k.Klasa WHEN 'AC'  THEN CASE WHEN s.DataUruchomienia < '2024-01-01' THEN 2 ELSE 1 END
                        WHEN 'DC'  THEN CASE WHEN s.DataUruchomienia < '2024-01-01' THEN 3 ELSE 4 END
                        ELSE 5 END AS ModelLadowarkiID
    FROM dbo.Stacje s JOIN konf k ON k.Typ = s.TypLokalizacji JOIN n ON n.i <= k.Ile
    UNION ALL   -- rozbudowa: ladowarki 300 kW na najwiekszych MOP
    SELECT s.StacjaID, s.Kod, x.Data, 9, 6
    FROM dbo.Stacje s JOIN (VALUES ('A2WIS', CAST('2024-02-12' AS date)), ('A2STR', '2025-05-05'), ('A1PIO', '2025-06-02')) AS x (Kod, Data) ON x.Kod = s.Kod
)
INSERT INTO dbo.Ladowarki (StacjaID, ModelLadowarkiID, NumerSeryjny, DataInstalacji)
SELECT StacjaID, ModelLadowarkiID,
       CONCAT(CASE ModelLadowarkiID WHEN 1 THEN 'VAC' WHEN 2 THEN 'AMP' WHEN 3 THEN 'ELD' WHEN 4 THEN 'VRP' WHEN 5 THEN 'NWH' ELSE 'ELU' END,
              '-', Kod, '-', ROW_NUMBER() OVER (PARTITION BY StacjaID ORDER BY DataInstalacji, ModelLadowarkiID, i)),
       DataInstalacji
FROM nowe
ORDER BY DataInstalacji, StacjaID, ModelLadowarkiID, i;

INSERT INTO dbo.ModelePojazdow (Marka, Model, Segment, PojemnoscBateriiKWh, MaksMocACkW, MaksMocDCkW) VALUES
 (N'Tesla', N'Model 3', N'średni', 60.0, 11, 170),          (N'Tesla', N'Model Y', N'SUV', 75.0, 11, 250),
 (N'Volkswagen', N'ID.3', N'kompakt', 58.0, 11, 120),       (N'Volkswagen', N'ID.4', N'SUV', 77.0, 11, 135),
 (N'Skoda', N'Enyaq', N'SUV', 77.0, 11, 135),               (N'Hyundai', N'Kona Electric', N'SUV', 64.0, 11, 100),
 (N'Hyundai', N'Ioniq 5', N'średni', 77.0, 11, 230),        (N'Kia', N'EV6', N'średni', 77.0, 11, 230),
 (N'Kia', N'Niro EV', N'SUV', 64.0, 11, 80),                (N'BMW', N'i4', N'średni', 81.0, 11, 205),
 (N'BMW', N'iX1', N'SUV', 65.0, 11, 130),                   (N'Mercedes', N'EQA', N'SUV', 67.0, 11, 100),
 (N'Audi', N'Q4 e-tron', N'SUV', 77.0, 11, 135),            (N'Renault', N'Megane E-Tech', N'kompakt', 60.0, 22, 130),
 (N'Renault', N'Zoe', N'mały', 52.0, 22, 50),               (N'Fiat', N'500e', N'miejski', 42.0, 11, 85),
 (N'MG', N'MG4', N'kompakt', 61.0, 11, 135),                (N'BYD', N'Atto 3', N'SUV', 60.0, 11, 88),
 (N'Volvo', N'EX30', N'SUV', 64.0, 11, 153),                (N'Peugeot', N'e-208', N'mały', 48.0, 11, 100),
 (N'Dacia', N'Spring', N'miejski', 27.0, 7, 30),            (N'Polestar', N'2', N'średni', 75.0, 11, 205),
 (N'Ford', N'Mustang Mach-E', N'SUV', 88.0, 11, 150),       (N'Volkswagen', N'ID. Buzz', N'dostawczy', 77.0, 11, 170),
 (N'Mercedes', N'eVito', N'dostawczy', 60.0, 11, 80);

INSERT INTO dbo.PlanyTaryfowe (PlanID, Nazwa, OplataMiesieczna, RabatProcent, Opis) VALUES
 (1, N'Start', 0,  0, N'konto w aplikacji bez opłaty miesięcznej, ceny z cennika'),
 (2, N'Plus', 29, 15, N'abonament miesięczny, energia 15% taniej'),
 (3, N'Flota', 0, 20, N'umowa dla firm, energia 20% taniej, faktura zbiorcza');

INSERT INTO dbo.Cennik (KlasaMocy, ObowiazujeOd, ObowiazujeDo, CenaZaKWh, OplataPostojowaZaMin, DarmowyPostojMin) VALUES
 ('AC',  '2022-01-01', '2025-02-28', 1.79, 0,    0),  ('AC',  '2025-03-01', NULL, 1.89, 0,    0),
 ('DC',  '2022-01-01', '2025-02-28', 2.49, 0.40, 15), ('DC',  '2025-03-01', NULL, 2.59, 0.40, 15),
 ('HPC', '2022-01-01', '2025-02-28', 2.89, 0.60, 10), ('HPC', '2025-03-01', NULL, 2.99, 0.60, 10);

INSERT INTO dbo.KategorieAwarii (KategoriaID, Nazwa, Grupa) VALUES
 (1, N'Brak komunikacji z systemem', N'techniczna'), (2, N'Uszkodzony kabel lub złącze', N'techniczna'),
 (3, N'Awaria modułu mocy', N'techniczna'),          (4, N'Awaria terminala płatniczego', N'techniczna'),
 (5, N'Wandalizm', N'zewnętrzna'),                   (6, N'Brak zasilania z sieci', N'zewnętrzna');
GO
SELECT N'Miejscowosci' AS Tabela, COUNT(*) AS Wierszy FROM dbo.Miejscowosci
UNION ALL SELECT N'Stacje', COUNT(*) FROM dbo.Stacje
UNION ALL SELECT N'ModeleLadowarek', COUNT(*) FROM dbo.ModeleLadowarek
UNION ALL SELECT N'Ladowarki', COUNT(*) FROM dbo.Ladowarki
UNION ALL SELECT N'ModelePojazdow', COUNT(*) FROM dbo.ModelePojazdow
UNION ALL SELECT N'PlanyTaryfowe', COUNT(*) FROM dbo.PlanyTaryfowe
UNION ALL SELECT N'Cennik', COUNT(*) FROM dbo.Cennik
UNION ALL SELECT N'KategorieAwarii', COUNT(*) FROM dbo.KategorieAwarii;
GO
