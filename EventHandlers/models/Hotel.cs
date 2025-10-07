using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EventHandlers.models
{
    public class Hotel
    {
        public string UserId { get; set; }

        public string HotelId { get; set; }

        public string Name { get; set; }

        public int Price { get; set; }

        public int Rating { get; set; }

        public string CityName { get; set; }

        public string FileName { get; set; }
    
        public DateTime CreationTime { get; set; } 
    }
}