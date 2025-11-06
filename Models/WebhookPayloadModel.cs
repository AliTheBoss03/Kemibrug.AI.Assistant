using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kemibrug.AI.Assistant.Models
{
    public class WebhookPayload
    {
        // For direct { "id": 123 } payload
        public int? Id { get; set; }

        // For the Azure DevOps service hook payload
        public ResourceContainer? Resource { get; set; }
    }

}
