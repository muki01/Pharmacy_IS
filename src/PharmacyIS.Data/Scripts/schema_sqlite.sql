-- =====================================================================
--  Информационна система за аптека
--  Схема на базата от данни за СУБД SQLite
--  Курсов проект по дисциплина „Информационни системи“, вариант 4
-- =====================================================================

-- Номенклатура „Роли на потребителите“
CREATE TABLE IF NOT EXISTS Roles (
    RoleId      INTEGER PRIMARY KEY,
    Code        TEXT NOT NULL UNIQUE,
    Name        TEXT NOT NULL
);

-- Потребители (служители на аптеката)
CREATE TABLE IF NOT EXISTS Users (
    UserId       INTEGER PRIMARY KEY AUTOINCREMENT,
    Username     TEXT    NOT NULL UNIQUE,
    PasswordHash TEXT    NOT NULL,
    PasswordSalt TEXT    NOT NULL,
    FullName     TEXT    NOT NULL,
    RoleId       INTEGER NOT NULL,
    IsActive     INTEGER NOT NULL DEFAULT 1,
    CreatedAt    TEXT    NOT NULL,
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles (RoleId)
);

-- Номенклатура „Лекарствени форми“
CREATE TABLE IF NOT EXISTS DosageForms (
    FormId INTEGER PRIMARY KEY AUTOINCREMENT,
    Code   TEXT NOT NULL UNIQUE,
    Name   TEXT NOT NULL
);

-- Номенклатура „Активни съставки“
CREATE TABLE IF NOT EXISTS ActiveIngredients (
    IngredientId INTEGER PRIMARY KEY AUTOINCREMENT,
    Code         TEXT NOT NULL UNIQUE,
    Name         TEXT NOT NULL
);

-- Доставчици
CREATE TABLE IF NOT EXISTS Suppliers (
    SupplierId    INTEGER PRIMARY KEY AUTOINCREMENT,
    Code          TEXT    NOT NULL UNIQUE,
    Name          TEXT    NOT NULL,
    Bulstat       TEXT,
    ContactPerson TEXT,
    Phone         TEXT,
    Email         TEXT,
    Address       TEXT,
    IsActive      INTEGER NOT NULL DEFAULT 1
);

-- Каталог на лекарствените продукти
CREATE TABLE IF NOT EXISTS Medicines (
    MedicineId           INTEGER PRIMARY KEY AUTOINCREMENT,
    Code                 TEXT    NOT NULL UNIQUE,
    Name                 TEXT    NOT NULL,
    IngredientId         INTEGER NOT NULL,
    FormId               INTEGER NOT NULL,
    Manufacturer         TEXT,
    Strength             TEXT,
    Price                REAL    NOT NULL CHECK (Price >= 0),
    RequiresPrescription INTEGER NOT NULL DEFAULT 0,
    MinStock             INTEGER NOT NULL DEFAULT 10 CHECK (MinStock >= 0),
    Barcode              TEXT,
    IsActive             INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT FK_Medicines_Ingredients FOREIGN KEY (IngredientId) REFERENCES ActiveIngredients (IngredientId),
    CONSTRAINT FK_Medicines_Forms       FOREIGN KEY (FormId)       REFERENCES DosageForms (FormId)
);

-- Партиди – носител на наличността и на срока на годност
CREATE TABLE IF NOT EXISTS Batches (
    BatchId       INTEGER PRIMARY KEY AUTOINCREMENT,
    MedicineId    INTEGER NOT NULL,
    BatchNumber   TEXT    NOT NULL,
    ExpiryDate    TEXT    NOT NULL,
    Quantity      INTEGER NOT NULL DEFAULT 0 CHECK (Quantity >= 0),
    PurchasePrice REAL    NOT NULL DEFAULT 0 CHECK (PurchasePrice >= 0),
    CreatedAt     TEXT    NOT NULL,
    CONSTRAINT FK_Batches_Medicines FOREIGN KEY (MedicineId) REFERENCES Medicines (MedicineId),
    CONSTRAINT UQ_Batches UNIQUE (MedicineId, BatchNumber, ExpiryDate)
);

-- Документ „Доставка“ – заглавна част
CREATE TABLE IF NOT EXISTS Deliveries (
    DeliveryId    INTEGER PRIMARY KEY AUTOINCREMENT,
    DocNumber     TEXT    NOT NULL UNIQUE,
    SupplierId    INTEGER NOT NULL,
    UserId        INTEGER NOT NULL,
    DeliveryDate  TEXT    NOT NULL,
    InvoiceNumber TEXT,
    TotalAmount   REAL    NOT NULL DEFAULT 0,
    Status        INTEGER NOT NULL DEFAULT 1,
    Note          TEXT,
    CONSTRAINT FK_Deliveries_Suppliers FOREIGN KEY (SupplierId) REFERENCES Suppliers (SupplierId),
    CONSTRAINT FK_Deliveries_Users     FOREIGN KEY (UserId)     REFERENCES Users (UserId)
);

-- Документ „Доставка“ – редове
CREATE TABLE IF NOT EXISTS DeliveryItems (
    DeliveryItemId INTEGER PRIMARY KEY AUTOINCREMENT,
    DeliveryId     INTEGER NOT NULL,
    MedicineId     INTEGER NOT NULL,
    BatchNumber    TEXT    NOT NULL,
    ExpiryDate     TEXT    NOT NULL,
    Quantity       INTEGER NOT NULL CHECK (Quantity > 0),
    UnitPrice      REAL    NOT NULL CHECK (UnitPrice >= 0),
    LineTotal      REAL    NOT NULL,
    CONSTRAINT FK_DeliveryItems_Deliveries FOREIGN KEY (DeliveryId) REFERENCES Deliveries (DeliveryId) ON DELETE CASCADE,
    CONSTRAINT FK_DeliveryItems_Medicines  FOREIGN KEY (MedicineId) REFERENCES Medicines (MedicineId)
);

-- Документ „Продажба“ – заглавна част
CREATE TABLE IF NOT EXISTS Sales (
    SaleId      INTEGER PRIMARY KEY AUTOINCREMENT,
    DocNumber   TEXT    NOT NULL UNIQUE,
    UserId      INTEGER NOT NULL,
    SaleDate    TEXT    NOT NULL,
    TotalAmount REAL    NOT NULL DEFAULT 0,
    TotalCost   REAL    NOT NULL DEFAULT 0,
    PaymentType INTEGER NOT NULL DEFAULT 1,
    Status      INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT FK_Sales_Users FOREIGN KEY (UserId) REFERENCES Users (UserId)
);

-- Документ „Продажба“ – редове
CREATE TABLE IF NOT EXISTS SaleItems (
    SaleItemId INTEGER PRIMARY KEY AUTOINCREMENT,
    SaleId     INTEGER NOT NULL,
    MedicineId INTEGER NOT NULL,
    BatchId    INTEGER NOT NULL,
    Quantity   INTEGER NOT NULL CHECK (Quantity > 0),
    UnitPrice  REAL    NOT NULL CHECK (UnitPrice >= 0),
    UnitCost   REAL    NOT NULL DEFAULT 0,
    LineTotal  REAL    NOT NULL,
    CONSTRAINT FK_SaleItems_Sales     FOREIGN KEY (SaleId)     REFERENCES Sales (SaleId) ON DELETE CASCADE,
    CONSTRAINT FK_SaleItems_Medicines FOREIGN KEY (MedicineId) REFERENCES Medicines (MedicineId),
    CONSTRAINT FK_SaleItems_Batches   FOREIGN KEY (BatchId)    REFERENCES Batches (BatchId)
);

-- Протокол за брак – заглавна част
CREATE TABLE IF NOT EXISTS WriteOffs (
    WriteOffId   INTEGER PRIMARY KEY AUTOINCREMENT,
    DocNumber    TEXT    NOT NULL UNIQUE,
    UserId       INTEGER NOT NULL,
    WriteOffDate TEXT    NOT NULL,
    Reason       TEXT    NOT NULL,
    TotalCost    REAL    NOT NULL DEFAULT 0,
    Status       INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT FK_WriteOffs_Users FOREIGN KEY (UserId) REFERENCES Users (UserId)
);

-- Протокол за брак – редове
CREATE TABLE IF NOT EXISTS WriteOffItems (
    WriteOffItemId INTEGER PRIMARY KEY AUTOINCREMENT,
    WriteOffId     INTEGER NOT NULL,
    MedicineId     INTEGER NOT NULL,
    BatchId        INTEGER NOT NULL,
    Quantity       INTEGER NOT NULL CHECK (Quantity > 0),
    UnitCost       REAL    NOT NULL DEFAULT 0,
    LineTotal      REAL    NOT NULL,
    CONSTRAINT FK_WriteOffItems_WriteOffs FOREIGN KEY (WriteOffId) REFERENCES WriteOffs (WriteOffId) ON DELETE CASCADE,
    CONSTRAINT FK_WriteOffItems_Medicines FOREIGN KEY (MedicineId) REFERENCES Medicines (MedicineId),
    CONSTRAINT FK_WriteOffItems_Batches   FOREIGN KEY (BatchId)    REFERENCES Batches (BatchId)
);

-- Журнал на складовите движения
CREATE TABLE IF NOT EXISTS StockMovements (
    MovementId   INTEGER PRIMARY KEY AUTOINCREMENT,
    MedicineId   INTEGER NOT NULL,
    BatchId      INTEGER NOT NULL,
    MovementType INTEGER NOT NULL,
    Quantity     INTEGER NOT NULL,
    DocType      TEXT    NOT NULL,
    DocNumber    TEXT    NOT NULL,
    MovementDate TEXT    NOT NULL,
    UserId       INTEGER NOT NULL,
    CONSTRAINT FK_Movements_Medicines FOREIGN KEY (MedicineId) REFERENCES Medicines (MedicineId),
    CONSTRAINT FK_Movements_Batches   FOREIGN KEY (BatchId)    REFERENCES Batches (BatchId),
    CONSTRAINT FK_Movements_Users     FOREIGN KEY (UserId)     REFERENCES Users (UserId)
);

-- Одитен дневник
CREATE TABLE IF NOT EXISTS AuditLog (
    LogId     INTEGER PRIMARY KEY AUTOINCREMENT,
    EventDate TEXT    NOT NULL,
    Username  TEXT    NOT NULL,
    Action    TEXT    NOT NULL,
    Details   TEXT,
    IsSuccess INTEGER NOT NULL DEFAULT 1
);

-- Системни настройки (прагове за предупрежденията и др.)
CREATE TABLE IF NOT EXISTS Settings (
    SettingKey   TEXT PRIMARY KEY,
    SettingValue TEXT NOT NULL,
    Description  TEXT
);

-- --------------------------------------------------------------------
-- Индекси за ускоряване на най-често изпълняваните заявки
-- --------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS IX_Medicines_Name    ON Medicines (Name);
CREATE INDEX IF NOT EXISTS IX_Medicines_Barcode ON Medicines (Barcode);
CREATE INDEX IF NOT EXISTS IX_Batches_Medicine  ON Batches (MedicineId);
CREATE INDEX IF NOT EXISTS IX_Batches_Expiry    ON Batches (ExpiryDate);
CREATE INDEX IF NOT EXISTS IX_Sales_Date        ON Sales (SaleDate);
CREATE INDEX IF NOT EXISTS IX_SaleItems_Sale    ON SaleItems (SaleId);
CREATE INDEX IF NOT EXISTS IX_Deliveries_Date   ON Deliveries (DeliveryDate);
CREATE INDEX IF NOT EXISTS IX_Movements_Date    ON StockMovements (MovementDate);
CREATE INDEX IF NOT EXISTS IX_Movements_Batch   ON StockMovements (BatchId);

-- --------------------------------------------------------------------
-- Изгледи: обобщена наличност и продукти под минималния запас
-- --------------------------------------------------------------------
CREATE VIEW IF NOT EXISTS v_MedicineStock AS
SELECT
    m.MedicineId,
    m.Code,
    m.Name,
    m.MinStock,
    m.Price,
    COALESCE(SUM(b.Quantity), 0) AS StockQuantity,
    MIN(CASE WHEN b.Quantity > 0 THEN b.ExpiryDate END) AS NearestExpiry
FROM Medicines m
LEFT JOIN Batches b ON b.MedicineId = m.MedicineId AND b.Quantity > 0
WHERE m.IsActive = 1
GROUP BY m.MedicineId, m.Code, m.Name, m.MinStock, m.Price;

CREATE VIEW IF NOT EXISTS v_LowStock AS
SELECT * FROM v_MedicineStock WHERE StockQuantity <= MinStock;
