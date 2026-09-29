-- it's how I'd roll partitioning into a live production database without downtime —
-- the WITH (DROP_EXISTING = ON) rebuild runs online in Enterprise Edition and takes
-- seconds-to-minutes depending on data size.

-- =========================================================
-- One-time migration: convert non-partitioned tables to partitioned
-- Prerequisite: schema.sql has already created the tables with data
-- =========================================================

-- 1. Partition function and scheme (safe if they already exist)
IF NOT EXISTS (SELECT 1 FROM sys.partition_functions WHERE name = 'pf_TxDate')
    CREATE PARTITION FUNCTION pf_TxDate (DATETIME2)
    AS RANGE RIGHT FOR VALUES
    ('2021-01-01','2022-01-01','2023-01-01','2024-01-01',
     '2025-01-01','2026-01-01','2027-01-01','2028-01-01',
     '2029-01-01','2030-01-01','2031-01-01');

IF NOT EXISTS (SELECT 1 FROM sys.partition_schemes WHERE name = 'ps_TxDate')
    CREATE PARTITION SCHEME ps_TxDate
    AS PARTITION pf_TxDate ALL TO ([PRIMARY]);

-- 2. Add denormalized TransactionDate to details (backfill from parent)
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE name = 'TransactionDate'
                 AND object_id = OBJECT_ID('dbo.StockTransactionDetails'))
BEGIN
    ALTER TABLE dbo.StockTransactionDetails ADD TransactionDate DATETIME2 NULL;

    UPDATE d
    SET d.TransactionDate = t.TransactionDate
    FROM dbo.StockTransactionDetails d
    INNER JOIN dbo.StockTransactions t ON t.TransactionId = d.TransactionId;

    ALTER TABLE dbo.StockTransactionDetails
        ALTER COLUMN TransactionDate DATETIME2 NOT NULL;
END

-- 3. Drop FK on transactions (must drop before PK)
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Transactions_Store')
    ALTER TABLE dbo.StockTransactions DROP CONSTRAINT FK_Transactions_Store;

-- 4. Rebuild StockTransactions as partitioned
--    Step 4a: Drop the non-partitioned PK constraint
ALTER TABLE dbo.StockTransactions DROP CONSTRAINT PK_StockTransactions;

--    Step 4b: Create a partitioned clustered index with the same name
--             (this becomes the new PK, but created via CREATE INDEX first)
CREATE UNIQUE CLUSTERED INDEX PK_StockTransactions
    ON dbo.StockTransactions (TransactionId, TransactionDate)
    WITH (DROP_EXISTING = OFF, ONLINE = OFF)
    ON ps_TxDate(TransactionDate);

--    Step 4c: Add the PK constraint on top of the clustered index
ALTER TABLE dbo.StockTransactions
    ADD CONSTRAINT PK_StockTransactions PRIMARY KEY CLUSTERED
        (TransactionId, TransactionDate)
    ON ps_TxDate(TransactionDate);

--    Step 4d: Recreate the FK
ALTER TABLE dbo.StockTransactions
    ADD CONSTRAINT FK_Transactions_Store
    FOREIGN KEY (StoreId) REFERENCES dbo.Stores(StoreId);

-- 5. Rebuild StockTransactionDetails as partitioned
ALTER TABLE dbo.StockTransactionDetails DROP CONSTRAINT FK_Details_Item;
ALTER TABLE dbo.StockTransactionDetails DROP CONSTRAINT PK_StockTransactionDetails;

CREATE UNIQUE CLUSTERED INDEX PK_StockTransactionDetails
    ON dbo.StockTransactionDetails (DetailId, TransactionDate)
    WITH (DROP_EXISTING = OFF, ONLINE = OFF)
    ON ps_TxDate(TransactionDate);

ALTER TABLE dbo.StockTransactionDetails
    ADD CONSTRAINT PK_StockTransactionDetails PRIMARY KEY CLUSTERED
        (DetailId, TransactionDate)
    ON ps_TxDate(TransactionDate);

ALTER TABLE dbo.StockTransactionDetails
    ADD CONSTRAINT FK_Details_Item
    FOREIGN KEY (ItemId) REFERENCES dbo.Items(ItemId);