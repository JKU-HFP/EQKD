using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Windows.Shapes;


namespace EQKD_DesignClassLibrary
{
    public class VerticalProgressBar : ProgressBar
    {
        //public Brush brush;

        //public VerticalProgressBar()
        //{
        //    this.SetStyle(ControlStyles.UserPaint, true);
        //    brush = Brushes.ForestGreen;
        //}


        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x04;
                return cp;
            }

        }

        //protected override void OnPaint(PaintEventArgs e)
        //{
        //    //base.OnPaint(e);
        //    System.Drawing.Rectangle rectangle = e.ClipRectangle;
        //    rectangle.Width = (int)(rectangle.Width * ((double)Value / Maximum)) - 4;
        //    ProgressBarRenderer.DrawHorizontalBar(e.Graphics, e.ClipRectangle);
        //    rectangle.Height = Height - 4;
        //    e.Graphics.FillRectangle(brush, 2, 2, rectangle.Width, rectangle.Height);
        //}

    }

}
