// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Input;

namespace osu.Game.Graphics.UserInterfaceV2
{
    public partial class FormNumberBox : FormTextBox
    {
        private readonly bool allowDecimals;
        private readonly bool allowNegative;

        public FormNumberBox(bool allowDecimals = false, bool allowNegative = false)
        {
            this.allowDecimals = allowDecimals;
            this.allowNegative = allowNegative;
        }

        internal override InnerTextBox CreateTextBox() => new InnerNumberBox(allowDecimals, allowNegative)
        {
            SelectAllOnFocus = true,
        };

        internal partial class InnerNumberBox : InnerTextBox
        {
            private readonly bool allowNegative;

            public InnerNumberBox(bool allowDecimals, bool allowNegative = false)
            {
                this.allowNegative = allowNegative;
                InputProperties = new TextInputProperties(allowDecimals ? TextInputType.Decimal : TextInputType.Number, false);
            }

            protected override bool CanAddCharacter(char character)
                => character == '-' && allowNegative || base.CanAddCharacter(character);
        }
    }
}
