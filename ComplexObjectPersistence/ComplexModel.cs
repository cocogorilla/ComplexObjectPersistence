using System;
using System.Collections.Generic;

namespace ComplexObjectPersistence
{
    public class ComplexModel
    {
        public class InnerComplexModel
        {
            public Guid InnerId { get; set; }
            public string InnerValue { get; set; }
            public int InnerKey { get; set; }
        }

        public Guid MasterId { get; set; }
        public string MasterValue { get; set; }
        public string MasterAlternateValue { get; set; }
        public IEnumerable<InnerComplexModel> InnerModels { get; set; }
    }
}
