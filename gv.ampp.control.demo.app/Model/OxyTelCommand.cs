using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gv.ampp.control.demo.app.Model
{
    internal class OxyTelCommand
    {
        /// <summary>
        /// Channel Index
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// An example of setting an Integer
        /// </summary>
        public int? Volume { get; set; }

        /// <summary>
        ///  An example of setting a category
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        ///  An example of setting a layer
        /// </summary>
        public string layer { get; set; }

        /// <summary>
        ///  An example of setting a logo
        /// </summary>
        public string Template { get; set; }

        /// <summary>
        /// An example of setting a boolean
        /// </summary>
        public bool? Active { get; set; }
    }
}
