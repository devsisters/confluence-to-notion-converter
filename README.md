# Confluence to Notion Converter

This open-source software project implements a tool that transforms XML data backup files from the Confluence cloud so that users can import the existing contents into Notion.

This project is still under development, and there is still a lot of work to be done to verify, implement, and test the functionality.

A lot of sharing and participation will help the project progress.

## Software Requirements

To build and test this project, you need the [.NET 6 SDK](https://dot.net/).

Windows, Linux, and macOS are supported as of operating systems.

## How to use

1. In the Confluence cloud, go to the space you want to export data to and the Space Settings - Space Management - Space Export menu.

1. Next, select all the pages in the custom export and send the export request. Wait for the operation to complete.

   **Watch out**: If you close your browser or go to another page, you may not be able to download the ZIP file.

1. After downloading the ZIP file, refer to the command-line synopsis above and run the tool.

1. After logging into Notion, select the Import - Confluence menu. And upload the converted ZIP file.

   **Watch out**: As of May 2022, Notion is limiting the maximum size of a ZIP file that can be uploaded in this way to approximately 2.5 GiB.

1. Check the results on the Notion page with the imported data. Each page also has the address of the original Confluence cloud page, so you can collate it if needed.

## Limitations

This tool is not fully developed and is unfinished. Therefore, the following functions do not work properly and require further development.

- The hierarchy of pages is not preserved.

- Not all custom Confluence extension tags are supported.

- Attachments other than image files are not processed.

- Image files are lossily compressed to a level of 75% compared to the original using the ImageMagick library by intent. If image quality is important, reconsider its use.

- The verbose log feature is not yet implemented.

In addition, there may be more restrictions. All of these constraints require further development.

## Command-line Tool Synopsis

```bash
$ dotnet run -- --help

Description:
  Confluence to Notion data converter

Usage:
  conf2notion [options]

Options:
  --input-xml-zip-file <input-xml-zip-file>      The file system path where the ZIP file in XML format created by the Confluence Space export function is stored.
  --confluence-domain <confluence-domain>        The domain name of the Confluence cloud service (e.g., acme.atlassian.net).
  --output-html-zip-file <output-html-zip-file>  File system path to save the converted ZIP file.
  --verbose                                      Toggle verbosity [default: False]
  --version                                      Show version information
  -?, -h, --help                                 Show help and usage information
```

Three parameters must be specified to run this tool.

- The `--input-xml-zip-file` switch must specify the file system path of the ZIP file containing the XML data created by the space export feature in the Confluence cloud. URLs are not supported.

- The `--confluence-domain` switch must specify the domain name of the Confluence cloud service. For example, if you have a team named `acme`, you should specify `acme.atlassian.net`.

- The `--output-html-zip-file` switch must specify the path to the ZIP file that will be created after conversion. If there is a file in the path, the execution of the program is stopped.

## Contributions

This project is an open-source software project, and you can contribute to code modifications through our GitHub repository.

## Disclaimer

The code of this project is provided as-is, and we are not responsible for any damages that may arise from the execution or utilization of the code.

If using this tool is difficult or not appropriate, please consider that it may be more appropriate to contact technical support directly with Atlassian or Notion.

## License

The source code for this project is provided under the Apache-2.0 license.

[Please check here for more details.](LICENSE.txt)
