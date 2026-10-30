
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UFS_StayConnect.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddIndividualCustomerCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the Payments.CustomerChargeID column only if it does not exist.
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Payments', 'CustomerChargeID') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Payments]
                    ADD [CustomerChargeID] int NULL;
                END
            ");

            // Create the CustomerCharges table only if it does not exist.
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.CustomerCharges', 'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[CustomerCharges]
                    (
                        [CustomerChargeID] int IDENTITY(1,1) NOT NULL,
                        [UserID] nvarchar(450) NOT NULL,
                        [ApplicationID] int NOT NULL,
                        [AccommodationID] int NOT NULL,
                        [PaymentTypeID] int NOT NULL,
                        [Title] nvarchar(120) NOT NULL,
                        [Description] nvarchar(600) NULL,
                        [Amount] decimal(18,2) NOT NULL,
                        [AssignedAt] datetime2 NOT NULL,
                        [DueDate] datetime2 NOT NULL,
                        [IsActive] bit NOT NULL,
                        [AdminNote] nvarchar(500) NULL,

                        CONSTRAINT [PK_CustomerCharges]
                            PRIMARY KEY ([CustomerChargeID]),

                        CONSTRAINT [FK_CustomerCharges_Accommodations_AccommodationID]
                            FOREIGN KEY ([AccommodationID])
                            REFERENCES [dbo].[Accommodations] ([AccommodationID])
                            ON DELETE NO ACTION,

                        CONSTRAINT [FK_CustomerCharges_Applications_ApplicationID]
                            FOREIGN KEY ([ApplicationID])
                            REFERENCES [dbo].[Applications] ([ApplicationID])
                            ON DELETE NO ACTION,

                        CONSTRAINT [FK_CustomerCharges_AspNetUsers_UserID]
                            FOREIGN KEY ([UserID])
                            REFERENCES [dbo].[AspNetUsers] ([Id])
                            ON DELETE NO ACTION,

                        CONSTRAINT [FK_CustomerCharges_PaymentTypes_PaymentTypeID]
                            FOREIGN KEY ([PaymentTypeID])
                            REFERENCES [dbo].[PaymentTypes] ([PaymentTypeID])
                            ON DELETE NO ACTION
                    );
                END
            ");

            // Create the Payments index only if it does not already exist.
            migrationBuilder.Sql(@"
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = 'IX_Payments_CustomerChargeID'
                      AND [object_id] = OBJECT_ID('dbo.Payments')
                )
                BEGIN
                    CREATE INDEX [IX_Payments_CustomerChargeID]
                    ON [dbo].[Payments] ([CustomerChargeID]);
                END
            ");

            // Create the CustomerCharges indexes only when missing.
            migrationBuilder.Sql(@"
                IF NOT EXISTS
                (
                    SELECT 1 FROM sys.indexes
                    WHERE [name] = 'IX_CustomerCharges_AccommodationID'
                      AND [object_id] = OBJECT_ID('dbo.CustomerCharges')
                )
                BEGIN
                    CREATE INDEX [IX_CustomerCharges_AccommodationID]
                    ON [dbo].[CustomerCharges] ([AccommodationID]);
                END

                IF NOT EXISTS
                (
                    SELECT 1 FROM sys.indexes
                    WHERE [name] = 'IX_CustomerCharges_ApplicationID'
                      AND [object_id] = OBJECT_ID('dbo.CustomerCharges')
                )
                BEGIN
                    CREATE INDEX [IX_CustomerCharges_ApplicationID]
                    ON [dbo].[CustomerCharges] ([ApplicationID]);
                END

                IF NOT EXISTS
                (
                    SELECT 1 FROM sys.indexes
                    WHERE [name] = 'IX_CustomerCharges_PaymentTypeID'
                      AND [object_id] = OBJECT_ID('dbo.CustomerCharges')
                )
                BEGIN
                    CREATE INDEX [IX_CustomerCharges_PaymentTypeID]
                    ON [dbo].[CustomerCharges] ([PaymentTypeID]);
                END

                IF NOT EXISTS
                (
                    SELECT 1 FROM sys.indexes
                    WHERE [name] = 'IX_CustomerCharges_UserID'
                      AND [object_id] = OBJECT_ID('dbo.CustomerCharges')
                )
                BEGIN
                    CREATE INDEX [IX_CustomerCharges_UserID]
                    ON [dbo].[CustomerCharges] ([UserID]);
                END
            ");

            // Add the Payments foreign key only if it does not already exist.
            migrationBuilder.Sql(@"
                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE [name] =
                        'FK_Payments_CustomerCharges_CustomerChargeID'
                      AND [parent_object_id] = OBJECT_ID('dbo.Payments')
                )
                BEGIN
                    ALTER TABLE [dbo].[Payments]
                    ADD CONSTRAINT
                        [FK_Payments_CustomerCharges_CustomerChargeID]
                    FOREIGN KEY ([CustomerChargeID])
                    REFERENCES [dbo].[CustomerCharges] ([CustomerChargeID])
                    ON DELETE NO ACTION;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the Payments foreign key if it exists.
            migrationBuilder.Sql(@"
                IF EXISTS
                (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE [name] =
                        'FK_Payments_CustomerCharges_CustomerChargeID'
                      AND [parent_object_id] = OBJECT_ID('dbo.Payments')
                )
                BEGIN
                    ALTER TABLE [dbo].[Payments]
                    DROP CONSTRAINT
                        [FK_Payments_CustomerCharges_CustomerChargeID];
                END
            ");

            // Remove the Payments index if it exists.
            migrationBuilder.Sql(@"
                IF EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = 'IX_Payments_CustomerChargeID'
                      AND [object_id] = OBJECT_ID('dbo.Payments')
                )
                BEGIN
                    DROP INDEX [IX_Payments_CustomerChargeID]
                    ON [dbo].[Payments];
                END
            ");

            // Drop CustomerCharges if it exists.
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.CustomerCharges', 'U') IS NOT NULL
                BEGIN
                    DROP TABLE [dbo].[CustomerCharges];
                END
            ");

            // Remove the column if it exists.
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Payments', 'CustomerChargeID') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[Payments]
                    DROP COLUMN [CustomerChargeID];
                END
            ");
        }
    }
}
