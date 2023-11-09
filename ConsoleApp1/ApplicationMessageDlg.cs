using Opc.Ua.Configuration;

using System.Diagnostics;

namespace ControlsServer
{
    internal class ApplicationMessageDlg : IApplicationMessageDlg
    {
        private readonly TextWriter output;

        public ApplicationMessageDlg(TextWriter m_output)
        {
            output = m_output;
        }

        public override void Message(string text, bool ask = false)
        {
            Debug.WriteLine(text);
            //throw new NotImplementedException();
        }

        public override Task<bool> ShowAsync()
        {
            Debug.WriteLine("show async called");
            return Task.FromResult(true);
        }
    }
}