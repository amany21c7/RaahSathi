using System;
using System.Collections.Generic;

namespace RaahSathi.Models
{
    public class MechanicSubscriptionStatusDto
    {
        public int MechanicId { get; set; }
        public string MechanicName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string ShopName { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public DateTime RegisteredAt { get; set; }
        public int DaysSinceJoined { get; set; }
        public int CompletedJobsCount { get; set; }
        public string Status { get; set; } = "Trial"; // "Active", "Trial", "Due", "Exempt", "Disabled"
        public bool IsSubscriptionRequired { get; set; }
        public DateTime? ValidTill { get; set; }
        public double AmountPaidTotal { get; set; }
        public int RemainingTrialDays { get; set; }
        public DateTime? LastPaymentDate { get; set; }
    }

    public class AdminSubscriptionsPageViewModel
    {
        public bool IsMasterEnabled { get; set; }
        public double MonthlyFee { get; set; }
        public int FreeTrialDays { get; set; }
        public int MinJobsRequired { get; set; }

        public int TotalMechanics { get; set; }
        public int ActiveSubscribersCount { get; set; }
        public int FreeTrialCount { get; set; }
        public int DueCount { get; set; }
        public int ExemptCount { get; set; }
        public double TotalSubscriptionRevenue { get; set; }

        // Server-side Pagination & Filter Properties
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 100;
        public int TotalRecords { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalRecords / PageSize) : 1;
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
        public string StatusFilter { get; set; } = "all";
        public string SelectedCity { get; set; } = "all";
        public List<CityCountDto> AvailableCities { get; set; } = new();

        public List<MechanicSubscriptionStatusDto> Mechanics { get; set; } = new();
        public List<MechanicSubscription> RecentTransactions { get; set; } = new();
    }

    public class CityCountDto
    {
        public string CityName { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
