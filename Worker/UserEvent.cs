using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Worker
{
    public class UserEvent
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public Status Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
