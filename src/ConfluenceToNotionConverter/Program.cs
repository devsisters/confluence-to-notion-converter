namespace ConfluenceToNotionConverter
{
    internal static class Program
    {
        /// <summary>
        /// Confluence to Notion data converter
        /// </summary>
        /// <param name="inputXmlZipFile">The file system path where the ZIP file in XML format created by the Confluence Space export function is stored.</param>
        /// <param name="confluenceDomain">The domain name of the Confluence cloud service (e.g., acme.atlassian.net).</param>
        /// <param name="outputHtmlZipFile">File system path to save the converted ZIP file.</param>
        /// <param name="verbose">Toggle verbosity</param>
        private static void Main(
            FileInfo inputXmlZipFile,
            string confluenceDomain,
            FileInfo outputHtmlZipFile,
            bool verbose = false)
        {
            try
            {
                if (inputXmlZipFile == null)
                {
                    Console.Error.WriteLine("Please specify a Confluence-exported XML format ZIP file path.");
                    Environment.Exit(1);
                }

                if (confluenceDomain == null)
                {
                    Console.Error.WriteLine("Please specify a Confluence cloud domain name.");
                    Environment.Exit(1);
                }

                if (outputHtmlZipFile == null)
                {
                    Console.Error.WriteLine("Please specify a location of output file path.");
                    Environment.Exit(1);
                }

                XmlArchiveConverter.ConvertXmlArchiveToHtmlArchive(
                    inputXmlZipFile.FullName,
                    confluenceDomain,
                    outputHtmlZipFile.FullName,
                    verbose);

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unexpected error occurred. {ex.Message}");

                if (verbose)
                    Console.Error.WriteLine(ex.ToString());

                Environment.Exit(2);
            }
        }
    }
}
