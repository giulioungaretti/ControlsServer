using Opc.Ua.Configuration;

using System.Diagnostics;

namespace ConsoleApp1
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
            //throw new NotImplementedException();
        }
    }
}