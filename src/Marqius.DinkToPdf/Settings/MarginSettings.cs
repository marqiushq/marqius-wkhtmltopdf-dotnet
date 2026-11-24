using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace DinkToPdf
{
    /// <summary>The space around each page: top, right, bottom and left, in one unit.</summary>
    public class MarginSettings
    {
        /// <summary>The unit of the four sides: millimetres unless set.</summary>
        public Unit Unit { get; set; }

        /// <summary>The top margin, in <see cref="Unit"/>.</summary>
        public double? Top { get; set; }

        /// <summary>The bottom margin, in <see cref="Unit"/>.</summary>
        public double? Bottom { get; set; }

        /// <summary>The left margin, in <see cref="Unit"/>.</summary>
        public double? Left { get; set; }

        /// <summary>The right margin, in <see cref="Unit"/>.</summary>
        public double? Right { get; set; }

        /// <summary>No margins set, in millimetres: the program's own margins apply.</summary>
        public MarginSettings()
        {
            Unit = Unit.Millimeters;
        }

        /// <summary>The four margins in millimetres, in CSS's order: top, right, bottom, left.</summary>
        public MarginSettings(double top, double right, double bottom, double left) : this()
        {
            Top = top;

            Bottom = bottom;

            Left = left;

            Right = right;
        }

        /// <summary>A margin as the program reads it, such as "10mm" (null when not set).</summary>
        public string GetMarginValue(double? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            string strUnit = "in";

            switch (Unit)
            {
                case Unit.Inches: strUnit = "in";
                    break;
                case Unit.Millimeters: strUnit = "mm";
                    break;
                case Unit.Centimeters: strUnit = "cm";
                    break;
                default: strUnit = "in";
                    break;
            }

            return value.Value.ToString("0.##", CultureInfo.InvariantCulture) + strUnit;

        }
    }
}
