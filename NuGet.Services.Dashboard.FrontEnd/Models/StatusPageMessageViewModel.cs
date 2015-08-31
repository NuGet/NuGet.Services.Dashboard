using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace NuGetDashboard.Models
{
    public class StatusPageMessageViewModel
    {
        public StatusPageMessageViewModel()
        {
            When = DateTime.UtcNow;
        }

        public IEnumerable<SelectListItem> Environments
        {
            get
            {
                return new List<SelectListItem>
                {
                    new SelectListItem() {Text = "Development", Value = "dev", Selected = false},
                    new SelectListItem() {Text = "Staging", Value = "int", Selected = false},
                    new SelectListItem() {Text = "Production", Value = "prod", Selected = true}
                };
            }
        }

        public IEnumerable<SelectListItem> Prefixes
        {
            get
            {
                return new List<SelectListItem>
                {
                    new SelectListItem() {Text = "Notice", Value = "**Notice** - ", Selected = false},
                    new SelectListItem() {Text = "Investigating", Value = "**Investigating** - ", Selected = false},
                    new SelectListItem() {Text = "Resolved", Value = "**Resolved** - ", Selected = false},
                    new SelectListItem() {Text = "Custom prefix (add \"**Prefix** -\" to your message)", Value = "", Selected = false}
                };
            }
        }

        public IEnumerable<SelectListItem> StatusOverrides
        {
            get
            {
                return new List<SelectListItem>
                {
                    new SelectListItem() {Text = "(based on Pingdom)", Value = "", Selected = false},
                    new SelectListItem() {Text = "Up (green)", Value = "Up", Selected = false},
                    new SelectListItem() {Text = "Degraded (orange)", Value = "Degraded", Selected = false},
                    new SelectListItem() {Text = "Down (red)", Value = "Down", Selected = false}
                };
            }
        }

        [Required]
        public string Environment { get; set; }

        public DateTime When { get; set; }

        public string Prefix { get; set; }

        [Required]
        public string Message { get; set; }

        public string StatusOverride { get; set; }
    }
}