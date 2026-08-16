using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.Models
{
    public class ReferralProgramSetting
    {
        public int Id { get; set; }

        public bool IsMasterEnabled { get; set; } = true;

        // 1. Mechanic to Mechanic (M2M)
        public bool M2M_Enabled { get; set; } = true;
        public double M2M_ReferrerReward { get; set; } = 300.0;
        public double M2M_RefereeReward { get; set; } = 150.0;

        // 2. Mechanic to Customer (M2C)
        public bool M2C_Enabled { get; set; } = true;
        public double M2C_ReferrerReward { get; set; } = 150.0;
        public double M2C_RefereeReward { get; set; } = 100.0;

        // 3. Customer to Customer (C2C)
        public bool C2C_Enabled { get; set; } = true;
        public double C2C_ReferrerReward { get; set; } = 100.0;
        public double C2C_RefereeReward { get; set; } = 50.0;

        // 4. Customer to Mechanic (C2M)
        public bool C2M_Enabled { get; set; } = true;
        public double C2M_ReferrerReward { get; set; } = 250.0;
        public double C2M_RefereeReward { get; set; } = 100.0;

        // Conditions
        public double MinWithdrawalAmount { get; set; } = 100.0;
        public double MinJobAmountForReward { get; set; } = 150.0;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
