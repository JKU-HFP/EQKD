using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;


namespace EQKD_DesignClassLibrary
{
    public class StaticProgressbar : ProgressBar
    {
        public System.Drawing.Brush brush;

        public StaticProgressbar()
        {
            this.SetStyle(ControlStyles.UserPaint, true);
            //brush = Brushes.ForestGreen;
            brush = new SolidBrush(System.Drawing.Color.FromArgb(255, (byte)86, (byte)156, (byte)186));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            //base.OnPaint(e);
            Rectangle rectangle = e.ClipRectangle;
            rectangle.Width = (int)(rectangle.Width * ((double)Value / Maximum)) - 4;
            ProgressBarRenderer.DrawHorizontalBar(e.Graphics, e.ClipRectangle);
            rectangle.Height = Height - 4;
            e.Graphics.FillRectangle(brush, 2, 2, rectangle.Width, rectangle.Height);
        }

    }

}
