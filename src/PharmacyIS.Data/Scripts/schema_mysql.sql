-- =====================================================================
--  Информационна система за аптека
--  Схема на базата от данни за СУБД MySQL 8.x / MariaDB 10.x
--  Курсов проект по дисциплина „Информационни системи“, вариант 4
--
--  Скриптът се изпълнява върху вече създадена база, например:
--      CREATE DATABASE pharmacy_is
--          CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
-- =====================================================================

CREATE TABLE IF NOT EXISTS Roles (
    RoleId INT NOT NULL,
    Code   VARCHAR(20)  NOT NULL,
    Name   VARCHAR(50)  NOT NULL,
    PRIMARY KEY (RoleId),
    UNIQUE KEY UQ_Roles_Code (Code)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE IF NOT EXISTS Users (
    UserId       INT          NOT NULL AUTO_INCREMENT,
    Username     VARCHAR(50)  NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,
    PasswordSalt VARCHAR(255) NOT NULL,
    FullName     VARCHAR(100) NOT NULL,
    RoleId       INT          NOT NULL,
    IsActive     TINYINT(1)   NOT NULL DEFAULT 1,
    CreatedAt    DATETIME     NOT NULL,
    PRIMARY KEY (UserId),
    UNIQUE KEY UQ_Users_Username (Username),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles (RoleId)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE IF NOT EXISTS DosageForms (
    FormId INT         NOT NULL AUTO_INCREMENT,
    Code   VARCHAR(10) NOT NULL,
    Name   VARCHAR(50) NOT NULL,
    PRIMARY KEY (FormId),
    UNIQUE KEY UQ_DosageForms_Code (Code)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE IF NOT EXISTS ActiveIngredients (
    IngredientId INT          NOT NULL AUTO_INCREMENT,
    Code         VARCHAR(10)  NOT NULL,
    Name         VARCHAR(100) NOT NULL,
    PRIMARY KEY (IngredientId),
    UNIQUE KEY UQ_Ingredients_Code (Code)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE IF NOT EXISTS Suppliers (
    SupplierId    INT          NOT NULL AUTO_INCREMENT,
    Code          VARCHAR(10)  NOT NULL,
    Name          VARCHAR(100) NOT NULL,
    Bulstat       VARCHAR(20),
    ContactPerson VARCHAR(100),
    Phone         VARCHAR(30),
    Email         VARCHAR(100),
    Address       VARCHAR(200),
    IsActive      TINYINT(1)   NOT NULL DEFAULT 1,
    PRIMARY KEY (SupplierId),
    UNIQUE KEY UQ_Suppliers_Code (Code)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE IF NOT EXISTS Medicines (
    MedicineId           INT            NOT NULL AUTO_INCREMENT,
    Code                 VARCHAR(20)    NOT NULL,
    Name                 VARCHAR(150)   NOT NULL,
    IngredientId         INT            NOT NULL,
    FormId               INT            NOT NULL,
    Manufacturer         VARCHAR(100),
    Strength             VARCHAR(50),
    Price                DECIMAL(10, 2) NOT NULL,
    RequiresPrescription TINYINT(1)     NOT NULL DEFAULT 0,
    MinStock             INT            NOT NULL DEFAULT 10,
    Barcode              VARCHAR(20),
    IsActive             TINYINT(1)     NOT NULL DEFAULT 1,
    PRIMARY KEY (MedicineId),
    UNIQUE KEY UQ_Medicines_Code (Code),
    KEY IX_Medicines_Name (Name),
    KEY IX_Medicines_Barcode (Barcode),
    CONSTRAINT CK_Medicines_Price    CHECK (Price >= 0),
    CONSTRAINT CK_Medicines_MinStock CHECK (MinStock >= 0),
    CONSTRAINT FK_Medicines_Ingredients FOREIGN KEY (IngredientId) REFERENCES ActiveIngredients (IngredientId),
    CONSTRAINT FK_Medicines_Forms       FOREIGN KEY (FormId)       REFERENCES DosageForms (FormId)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE IF NOT EXISTS Batches (
    BatchId       INT            NOT NULL AUTO_INCREMENT,
    MedicineId    INT            NOT NULL,
    BatchNumber   VARCHAR(50)    NOT NULL,
    ExpiryDate    DATETIME       NOT NULL,
    Quantity      INT            NOT NULL DEFAULT 0,
    PurchasePrice DECIMAL(10, 2) NOT NULL DEFAULT 0,
    CreatedAt     DATETIME       NOT NULL,
    PRIMARY KEY (BatchId),
    UNIQUE KEY UQ_Batches (MedicineId, BatchNumber, ExpiryDate),
    KEY IX_Batches_Expiry (ExpiryDate),
    CONSTRAINT CK_Batches_Quantity CHECK (Quantity >= 0),
    CONSTRAINT CK_Batches_Price    CHECK (PurchasePrice >= 0),
    CONSTRAINT FK_Batches_Medicines FOREIGN KEY (MedicineId) REFERENCES Medicines (MedicineId)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE IF NOT EXISTS Deliveries (
    DeliveryId    INT            NOT NULL AUTO_INCREMENT,
    DocNumber     VARCHAR(30)    NOT NULL,
    SupplierId    INT            NOT NULL,
    UserId        INT            NOT NULL,
    DeliveryDate  DATETIME       NOT NULL,
    InvoiceNumber VARCHAR(30),
    TotalAmount   DECIMAL(12, 2) NOT NULL DEFAULT 0,
    Status        INT            NOT NULL DEFAULT 1,
    Note          VARCHAR(255),
    PRIMARY KEY (DeliveryId),
    UNIQUE KEY UQ_Deliveries_DocNumber (DocNumber),
    KEY IX_Deliveries_Date (DeliveryDate),
    CONSTRAINT FK_Deliveries_Suppliers FOREIGN KEY (SupplierId) REFERENCES Suppliers (SupplierId),
    CONSTRAINT FK_Deliveries_Users     FOREIGN KEY (UserId)     REFERENCES Users (UserId)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE IF NOT EXISTS DeliveryItems (
    DeliveryItemId INT            NOT NULL AUTO_INCREMENT,
    DeliveryId     INT            NOT NULL,
    MedicineId     INT            NOT NULL,
    BatchNumber    VARCHAR(50)    NOT NULL,
    ExpiryDate     DATETIME       NOT NULL,
    Quantity       INT            NOT NULL,
    UnitPrice      DECIMAL(10, 2) NOT NULL,
    LineTotal      DECIMAL(12, 2) NOT NULL,
    PRIMARY KEY (DeliveryItemId),
    CONSTRAINT CK_DeliveryItems_Quantity CHECK (Quantity > 0),
    CONSTRAINT CK_DeliveryItems_Price    CHECK (UnitPrice >= 0),
    CONSTRAINT FK_DeliveryItems_Deliveries FOREIGN KEY (DeliveryId) REFERENCES Deliveries (DeliveryId) ON DELETE CASCADE,
    CONSTRAINT FK_DeliveryItems_Medicines  FOREIGN KEY (MedicineId) REFERENCES Medicines (MedicineId)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE IF NOT EXISTS Sales (
    SaleId      INT            NOT NULL AUTO_INCREMENT,
    DocNumber   VARCHAR(30)    NOT NULL,
    UserId      INT            NOT NULL,
    SaleDate    DATETIME       NOT NULL,
    TotalAmount DECIMAL(12, 2) NOT NULL DEFAULT 0,
    TotalCost   DECIMAL(12, 2) NOT NULL DEFAULT 0,
    PaymentType INT            NOT NULL DEFAULT 1,
    Status      INT            NOT NULL DEFAULT 1,
    PRIMARY KEY (SaleId),
    UNIQUE KEY UQ_Sales_DocNumber (DocNumber),
    KEY IX_Sales_Date (SaleDate),
    CONSTRAINT FK_Sales_Users FOREIGN KEY (UserId) REFERENCES Users (UserId)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE IF NOT EXISTS SaleItems (
    SaleItemId INT            NOT NULL AUTO_INCREMENT,
    SaleId     INT            NOT NULL,
    MedicineId INT            NOT NULL,
    BatchId    INT            NOT NULL,
    Quantity   INT            NOT NULL,
    UnitPrice  DECIMAL(10, 2) NOT NULL,
    UnitCost   DECIMAL(10, 2) NOT NULL DEFAULT 0,
    LineTotal  DECIMAL(12, 2) NOT NULL,
    PRIMARY KEY (SaleItemId),
    KEY IX_SaleItems_Sale (SaleId),
    CONSTRAINT CK_SaleItems_Quantity CHECK (Quantity > 0),
    CONSTRAINT CK_SaleItems_Price    CHECK (UnitPrice >= 0),
    CONSTRAINT FK_SaleItems_Sales     FOREIGN KEY (SaleId)     REFERENCES Sales (SaleId) ON DELETE CASCADE,
    CONSTRAINT FK_SaleItems_Medicines FOREIGN KEY (MedicineId) REFERENCES Medicines (MedicineId),
    CONSTRAINT FK_SaleItems_Batches   FOREIGN KEY (BatchId)    REFERENCES Batches (BatchId)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE IF NOT EXISTS WriteOffs (
    WriteOffId   INT            NOT NULL AUTO_INCREMENT,
    DocNumber    VARCHAR(30)    NOT NULL,
    UserId       INT            NOT NULL,
    WriteOffDate DATETIME       NOT NULL,
    Reason       VARCHAR(255)   NOT NULL,
    TotalCost    DECIMAL(12, 2) NOT NULL DEFAULT 0,
    Status       INT            NOT NULL DEFAULT 1,
    PRIMARY KEY (WriteOffId),
    UNIQUE KEY UQ_WriteOffs_DocNumber (DocNumber),
    CONSTRAINT FK_WriteOffs_Users FOREIGN KEY (UserId) REFERENCES Users (UserId)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE IF NOT EXISTS WriteOffItems (
    WriteOffItemId INT            NOT NULL AUTO_INCREMENT,
    WriteOffId     INT            NOT NULL,
    MedicineId     INT            NOT NULL,
    BatchId        INT            NOT NULL,
    Quantity       INT            NOT NULL,
    UnitCost       DECIMAL(10, 2) NOT NULL DEFAULT 0,
    LineTotal      DECIMAL(12, 2) NOT NULL,
    PRIMARY KEY (WriteOffItemId),
    CONSTRAINT CK_WriteOffItems_Quantity CHECK (Quantity > 0),
    CONSTRAINT FK_WriteOffItems_WriteOffs FOREIGN KEY (WriteOffId) REFERENCES WriteOffs (WriteOffId) ON DELETE CASCADE,
    CONSTRAINT FK_WriteOffItems_Medicines FOREIGN KEY (MedicineId) REFERENCES Medicines (MedicineId),
    CONSTRAINT FK_WriteOffItems_Batches   FOREIGN KEY (BatchId)    REFERENCES Batches (BatchId)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE IF NOT EXISTS StockMovements (
    MovementId   INT         NOT NULL AUTO_INCREMENT,
    MedicineId   INT         NOT NULL,
    BatchId      INT         NOT NULL,
    MovementType INT         NOT NULL,
    Quantity     INT         NOT NULL,
    DocType      VARCHAR(30) NOT NULL,
    DocNumber    VARCHAR(30) NOT NULL,
    MovementDate DATETIME    NOT NULL,
    UserId       INT         NOT NULL,
    PRIMARY KEY (MovementId),
    KEY IX_Movements_Date (MovementDate),
    KEY IX_Movements_Batch (BatchId),
    CONSTRAINT FK_Movements_Medicines FOREIGN KEY (MedicineId) REFERENCES Medicines (MedicineId),
    CONSTRAINT FK_Movements_Batches   FOREIGN KEY (BatchId)    REFERENCES Batches (BatchId),
    CONSTRAINT FK_Movements_Users     FOREIGN KEY (UserId)     REFERENCES Users (UserId)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE IF NOT EXISTS AuditLog (
    LogId     INT          NOT NULL AUTO_INCREMENT,
    EventDate DATETIME     NOT NULL,
    Username  VARCHAR(50)  NOT NULL,
    Action    VARCHAR(50)  NOT NULL,
    Details   VARCHAR(255),
    IsSuccess TINYINT(1)   NOT NULL DEFAULT 1,
    PRIMARY KEY (LogId)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

CREATE TABLE IF NOT EXISTS Settings (
    SettingKey   VARCHAR(50)  NOT NULL,
    SettingValue VARCHAR(255) NOT NULL,
    Description  VARCHAR(255),
    PRIMARY KEY (SettingKey)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

-- --------------------------------------------------------------------
-- Изгледи: обобщена наличност и продукти под минималния запас
-- --------------------------------------------------------------------
CREATE OR REPLACE VIEW v_MedicineStock AS
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

CREATE OR REPLACE VIEW v_LowStock AS
SELECT * FROM v_MedicineStock WHERE StockQuantity <= MinStock;
