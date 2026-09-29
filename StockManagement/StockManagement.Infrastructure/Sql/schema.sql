IF OBJECT_ID('dbo.Stores','U') IS NULL
BEGIN
    CREATE TABLE dbo.Stores (
        StoreId   INT IDENTITY(1,1) PRIMARY KEY,
        StoreCode NVARCHAR(20)  NOT NULL UNIQUE,
        StoreName NVARCHAR(100) NOT NULL,
        IsActive  BIT           NOT NULL DEFAULT 1
    );
END

IF OBJECT_ID('dbo.Items','U') IS NULL
BEGIN
    CREATE TABLE dbo.Items (
        ItemId   INT IDENTITY(1,1) PRIMARY KEY,
        ItemCode NVARCHAR(30)  NOT NULL UNIQUE,
        ItemName NVARCHAR(150) NOT NULL,
        UOM      NVARCHAR(10)  NOT NULL DEFAULT 'PCS',
        IsActive BIT           NOT NULL DEFAULT 1
    );
END

IF OBJECT_ID('dbo.StockTransactions','U') IS NULL
BEGIN
    CREATE TABLE dbo.StockTransactions (
        TransactionId   BIGINT IDENTITY(1,1) PRIMARY KEY,
        StoreId         INT           NOT NULL FOREIGN KEY REFERENCES dbo.Stores(StoreId),
        TransactionDate DATETIME2     NOT NULL,
        TransactionType NVARCHAR(20)  NOT NULL,
        ReferenceNo     NVARCHAR(50)  NOT NULL DEFAULT '',
        CreatedBy       NVARCHAR(50)  NOT NULL DEFAULT 'system',
        CreatedAt       DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion      ROWVERSION
    );
    CREATE INDEX IX_StockTransactions_Date  ON dbo.StockTransactions(TransactionDate DESC);
    CREATE INDEX IX_StockTransactions_Store ON dbo.StockTransactions(StoreId, TransactionDate DESC);
END

IF OBJECT_ID('dbo.StockTransactionDetails','U') IS NULL
BEGIN
    CREATE TABLE dbo.StockTransactionDetails (
        DetailId      BIGINT IDENTITY(1,1) PRIMARY KEY,
        TransactionId BIGINT   NOT NULL FOREIGN KEY REFERENCES dbo.StockTransactions(TransactionId) ON DELETE CASCADE,
        ItemId        INT      NOT NULL FOREIGN KEY REFERENCES dbo.Items(ItemId),
        Quantity      DECIMAL(18,4) NOT NULL,
        UnitCost      DECIMAL(18,4) NOT NULL DEFAULT 0,
        ExpiryDate    DATE          NULL,
        IsDamaged     BIT           NOT NULL DEFAULT 0,
        Remarks       NVARCHAR(500) NULL
    );
    CREATE INDEX IX_Details_Transaction ON dbo.StockTransactionDetails(TransactionId);
    CREATE INDEX IX_Details_Item        ON dbo.StockTransactionDetails(ItemId);
END

IF OBJECT_ID('dbo.StockBalances','U') IS NULL
BEGIN
    CREATE TABLE dbo.StockBalances (
        StoreId    INT           NOT NULL FOREIGN KEY REFERENCES dbo.Stores(StoreId),
        ItemId     INT           NOT NULL FOREIGN KEY REFERENCES dbo.Items(ItemId),
        OnHandQty  DECIMAL(18,4) NOT NULL DEFAULT 0,
        RowVersion ROWVERSION,
        CONSTRAINT PK_StockBalances PRIMARY KEY (StoreId, ItemId)
    );
END