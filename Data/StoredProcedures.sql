-- RaahSathi Enterprise Stored Procedures for SQL Server / SSMS
-- Provides atomic, ACID-compliant, concurrency-safe transactions

-- 1. Atomic Job Payment & Wallet Credit
CREATE OR ALTER PROCEDURE dbo.sp_ProcessJobPayment
    @JobId INT,
    @PaymentId NVARCHAR(100),
    @Amount FLOAT,
    @AdminCommission FLOAT,
    @MechanicEarning FLOAT,
    @CommissionRate FLOAT,
    @PaymentStatus NVARCHAR(50) = 'Released'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Idempotency check: if payment already released/completed, return
        IF EXISTS (SELECT 1 FROM dbo.Payments WITH (UPDLOCK, HOLDLOCK) WHERE JobId = @JobId AND PaymentStatus IN ('Released', 'Completed'))
        BEGIN
            COMMIT TRANSACTION;
            SELECT 1 AS Success, 'Payment already released previously.' AS Message;
            RETURN;
        END

        -- Insert or Update Payment record
        IF EXISTS (SELECT 1 FROM dbo.Payments WITH (UPDLOCK) WHERE JobId = @JobId)
        BEGIN
            UPDATE dbo.Payments
            SET Amount = @Amount,
                PaymentStatus = @PaymentStatus,
                RazorpayPaymentId = @PaymentId,
                AdminCommissionAmount = @AdminCommission,
                MechanicEarningAmount = @MechanicEarning,
                CommissionRateUsed = @CommissionRate
            WHERE JobId = @JobId;
        END
        ELSE
        BEGIN
            INSERT INTO dbo.Payments (JobId, Amount, PaymentStatus, RazorpayPaymentId, AdminCommissionAmount, MechanicEarningAmount, CommissionRateUsed, CreatedAt)
            VALUES (@JobId, @Amount, @PaymentStatus, @PaymentId, @AdminCommission, @MechanicEarning, @CommissionRate, GETUTCDATE());
        END

        -- Update Mechanic Profile Wallet Balance & Total Jobs with Row Lock
        DECLARE @MechanicId INT;
        SELECT @MechanicId = MechanicId FROM dbo.Jobs WHERE Id = @JobId;

        IF (@MechanicId IS NOT NULL AND @MechanicId > 0)
        BEGIN
            UPDATE dbo.MechanicProfiles WITH (ROWLOCK)
            SET CurrentEarnings = CurrentEarnings + @MechanicEarning,
                TotalJobs = TotalJobs + 1
            WHERE UserId = @MechanicId;
        END

        -- Mark Job as Completed
        UPDATE dbo.Jobs WITH (ROWLOCK)
        SET Status = 'Completed',
            CompletedAt = GETUTCDATE()
        WHERE Id = @JobId;

        COMMIT TRANSACTION;

        SELECT 1 AS Success, 'Payment processed and wallet updated successfully.' AS Message;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        SELECT 0 AS Success, @ErrorMessage AS Message;
    END CATCH
END;
GO

-- 2. Atomic Mechanic Payout Withdrawal Request
CREATE OR ALTER PROCEDURE dbo.sp_RequestMechanicPayout
    @MechanicId INT,
    @Amount FLOAT,
    @PayoutMethod NVARCHAR(50),
    @AccountHolderName NVARCHAR(200) = NULL,
    @BankAccountNumber NVARCHAR(100) = NULL,
    @BankName NVARCHAR(200) = NULL,
    @IfscCode NVARCHAR(50) = NULL,
    @UpiId NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Lock and check Mechanic Profile wallet balance
        DECLARE @CurrentEarnings FLOAT = 0.0;
        SELECT @CurrentEarnings = CurrentEarnings 
        FROM dbo.MechanicProfiles WITH (UPDLOCK, ROWLOCK) 
        WHERE UserId = @MechanicId;

        IF (@CurrentEarnings IS NULL OR @CurrentEarnings < @Amount)
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 0 AS Success, 'Insufficient wallet balance for withdrawal.' AS Message, 0 AS PayoutRequestId, ISNULL(@CurrentEarnings, 0.0) AS RemainingBalance;
            RETURN;
        END

        -- Deduct balance from mechanic wallet atomically
        UPDATE dbo.MechanicProfiles WITH (ROWLOCK)
        SET CurrentEarnings = CurrentEarnings - @Amount
        WHERE UserId = @MechanicId;

        -- Insert Payout Request
        INSERT INTO dbo.MechanicPayoutRequests 
            (MechanicId, Amount, PayoutMethod, AccountHolderName, BankAccountNumber, BankName, IfscCode, UpiId, Status, CreatedAt, AdminRemarks, TransactionReference)
        VALUES 
            (@MechanicId, @Amount, @PayoutMethod, @AccountHolderName, @BankAccountNumber, @BankName, @IfscCode, @UpiId, 'Pending', GETUTCDATE(), '', '');

        DECLARE @NewRequestId INT = SCOPE_IDENTITY();

        COMMIT TRANSACTION;

        SELECT 1 AS Success, 'Payout request submitted successfully.' AS Message, @NewRequestId AS PayoutRequestId, (@CurrentEarnings - @Amount) AS RemainingBalance;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        SELECT 0 AS Success, @ErrorMessage AS Message, 0 AS PayoutRequestId, 0.0 AS RemainingBalance;
    END CATCH
END;
GO

-- 3. Atomic Admin Payout Approval or Rejection (With Automatic Refund)
CREATE OR ALTER PROCEDURE dbo.sp_ProcessMechanicPayout
    @PayoutRequestId INT,
    @AdminAction NVARCHAR(20), -- 'Approve' or 'Reject'
    @AdminRemarks NVARCHAR(500) = '',
    @TransactionReference NVARCHAR(100) = ''
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @CurrentStatus NVARCHAR(50);
        DECLARE @MechanicId INT;
        DECLARE @Amount FLOAT;

        SELECT @CurrentStatus = Status, @MechanicId = MechanicId, @Amount = Amount
        FROM dbo.MechanicPayoutRequests WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @PayoutRequestId;

        IF (@CurrentStatus IS NULL OR @CurrentStatus <> 'Pending')
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 0 AS Success, 'Payout request is either not found or already processed.' AS Message;
            RETURN;
        END

        IF (@AdminAction = 'Approve')
        BEGIN
            UPDATE dbo.MechanicPayoutRequests WITH (ROWLOCK)
            SET Status = 'Approved',
                ProcessedAt = GETUTCDATE(),
                AdminRemarks = ISNULL(@AdminRemarks, 'Payout Approved'),
                TransactionReference = ISNULL(@TransactionReference, '')
            WHERE Id = @PayoutRequestId;
        END
        ELSE IF (@AdminAction = 'Reject')
        BEGIN
            UPDATE dbo.MechanicPayoutRequests WITH (ROWLOCK)
            SET Status = 'Rejected',
                ProcessedAt = GETUTCDATE(),
                AdminRemarks = ISNULL(@AdminRemarks, 'Payout Rejected')
            WHERE Id = @PayoutRequestId;

            -- Refund balance back to mechanic wallet atomically
            UPDATE dbo.MechanicProfiles WITH (ROWLOCK)
            SET CurrentEarnings = CurrentEarnings + @Amount
            WHERE UserId = @MechanicId;
        END

        COMMIT TRANSACTION;

        SELECT 1 AS Success, 'Payout processed successfully.' AS Message;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        SELECT 0 AS Success, @ErrorMessage AS Message;
    END CATCH
END;
GO

-- 4. Atomic User & Mechanic Profile Update
CREATE OR ALTER PROCEDURE dbo.sp_UpdateUserProfile
    @UserId INT,
    @Name NVARCHAR(100),
    @ShopName NVARCHAR(200) = NULL,
    @ShopAddress NVARCHAR(300) = NULL,
    @City NVARCHAR(100) = NULL,
    @VehicleExpertise NVARCHAR(250) = NULL,
    @Specialization NVARCHAR(250) = NULL,
    @BankName NVARCHAR(200) = NULL,
    @BankAccountNumber NVARCHAR(100) = NULL,
    @IfscCode NVARCHAR(50) = NULL,
    @UpiId NVARCHAR(100) = NULL,
    @AccountHolderName NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.Users WITH (ROWLOCK)
        SET Name = @Name
        WHERE Id = @UserId;

        IF EXISTS (SELECT 1 FROM dbo.MechanicProfiles WHERE UserId = @UserId)
        BEGIN
            UPDATE dbo.MechanicProfiles WITH (ROWLOCK)
            SET ShopName = ISNULL(@ShopName, ShopName),
                ShopAddress = ISNULL(@ShopAddress, ShopAddress),
                City = ISNULL(@City, City),
                VehicleExpertise = ISNULL(@VehicleExpertise, VehicleExpertise),
                Specialization = ISNULL(@Specialization, Specialization),
                BankName = ISNULL(@BankName, BankName),
                BankAccountNumber = ISNULL(@BankAccountNumber, BankAccountNumber),
                IfscCode = ISNULL(@IfscCode, IfscCode),
                UpiId = ISNULL(@UpiId, UpiId),
                AccountHolderName = ISNULL(@AccountHolderName, AccountHolderName)
            WHERE UserId = @UserId;
        END

        COMMIT TRANSACTION;

        SELECT 1 AS Success, 'Profile updated successfully.' AS Message;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        SELECT 0 AS Success, @ErrorMessage AS Message;
    END CATCH
END;
GO
