using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RaahSathi.Models;

namespace RaahSathi.Services
{
    public static class ContactInfoHelper
    {
        // Thread-safe defaults with persistent fallback
        private static string _helplineNumber = "+91 9891819236";
        private static string _tollFreeNumber = "1800-102-7224";
        private static string _emergencySupportNumber = "+91 9536838103";
        private static string _whatsAppNumber = "+91 9891819236";
        private static string _supportEmail = "support.raahsathi@gmail.com";
        private static string _billingEmail = "billing@raahsathi.in";
        private static string _partnerHelplineNumber = "+91 9891819236";
        private static string _officeAddress = "Tower B, DLF Cyber City, Sector 24, Gurugram, Haryana - 122002";
        private static readonly object _lock = new object();

        public static string HelplineNumber => _helplineNumber;
        public static string HelplineNumberClean => CleanPhone(_helplineNumber);

        public static string TollFreeNumber => _tollFreeNumber;
        public static string TollFreeNumberClean => CleanPhone(_tollFreeNumber);

        public static string EmergencySupportNumber => _emergencySupportNumber;
        public static string EmergencySupportNumberClean => CleanPhone(_emergencySupportNumber);

        public static string WhatsAppNumber => _whatsAppNumber;
        public static string WhatsAppNumberClean => CleanWhatsApp(_whatsAppNumber);

        public static string SupportEmail => _supportEmail;
        public static string BillingEmail => _billingEmail;

        public static string PartnerHelplineNumber => _partnerHelplineNumber;
        public static string PartnerHelplineNumberClean => CleanPhone(_partnerHelplineNumber);

        public static string OfficeAddress => _officeAddress;

        public static void Initialize(IEnumerable<AdminSystemSetting> settings)
        {
            if (settings == null) return;
            lock (_lock)
            {
                foreach (var s in settings)
                {
                    UpdateSingleSetting(s.SettingKey, s.SettingValue);
                }
            }
        }

        public static void UpdateSetting(string key, string value)
        {
            lock (_lock)
            {
                UpdateSingleSetting(key, value);
            }
        }

        private static void UpdateSingleSetting(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            string cleanVal = value?.Trim() ?? string.Empty;
            
            switch (key.Trim())
            {
                case "HelplineNumber":
                case "CallingNumber":
                    if (!string.IsNullOrWhiteSpace(cleanVal)) _helplineNumber = cleanVal;
                    break;
                case "TollFreeNumber":
                    if (!string.IsNullOrWhiteSpace(cleanVal)) _tollFreeNumber = cleanVal;
                    break;
                case "EmergencySupportNumber":
                    if (!string.IsNullOrWhiteSpace(cleanVal)) _emergencySupportNumber = cleanVal;
                    break;
                case "WhatsAppNumber":
                case "WhatsAppNo":
                    if (!string.IsNullOrWhiteSpace(cleanVal)) _whatsAppNumber = cleanVal;
                    break;
                case "SupportEmail":
                case "EmailSender":
                    if (!string.IsNullOrWhiteSpace(cleanVal)) _supportEmail = cleanVal;
                    break;
                case "BillingEmail":
                    if (!string.IsNullOrWhiteSpace(cleanVal)) _billingEmail = cleanVal;
                    break;
                case "PartnerHelplineNumber":
                    if (!string.IsNullOrWhiteSpace(cleanVal)) _partnerHelplineNumber = cleanVal;
                    break;
                case "OfficeAddress":
                    if (!string.IsNullOrWhiteSpace(cleanVal)) _officeAddress = cleanVal;
                    break;
            }
        }

        private static string CleanPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "+919891819236";
            var clean = Regex.Replace(phone, @"[^\d+]", "");
            return clean.StartsWith("+") ? clean : (clean.Length == 10 ? "+91" + clean : clean);
        }

        private static string CleanWhatsApp(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "919891819236";
            var clean = Regex.Replace(phone, @"[^\d]", "");
            if (clean.Length == 10) return "91" + clean;
            return clean;
        }
    }
}
