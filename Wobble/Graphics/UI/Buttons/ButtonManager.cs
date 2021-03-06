using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wobble.Graphics.UI.Buttons
{
    internal static class ButtonManager
    {
        /// <summary>
        ///     The list of buttons that are currently drawn.
        /// </summary>
        internal static List<Button> Buttons { get; } = new List<Button>();

        /// <summary>
        ///     Adds a button to the manager.
        /// </summary>
        /// <param name="btn"></param>
        internal static void Add(Button btn) => Buttons.Add(btn);

        /// <summary>
        ///     Removes a button from the manager.
        /// </summary>
        /// <param name="btn"></param>
        internal static void Remove(Button btn) => Buttons.Remove(btn);
    }
}
