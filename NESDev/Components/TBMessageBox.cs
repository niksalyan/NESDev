

namespace NTOSDev.Libs
{
    public static class TBMessageBox
    {
        public static DialogResult Confirm(IWin32Window owner, string heading, string message)
        {
            var page = new TaskDialogPage()
            {
                Icon = TaskDialogIcon.Warning,
                Caption = "Confirm",
                Heading = heading,
                Text = message
            };

            var yesBtn = new TaskDialogButton("Yes") { Tag = DialogResult.Yes };
            var noBtn = new TaskDialogButton("No") { Tag = DialogResult.No };


            page.Buttons.Add(yesBtn);
            page.Buttons.Add(noBtn);

            return (DialogResult)TaskDialog.ShowDialog(owner, page).Tag;
        }

        public static void Info(IWin32Window owner, string heading, string message)
        {
            var page = new TaskDialogPage()
            {
                Icon = TaskDialogIcon.Information,
                Caption = "Information",
                Heading = heading,
                Text = message
            };

            page.Buttons.Add(TaskDialogButton.OK);

            TaskDialog.ShowDialog(owner, page);
        }
    }
}
