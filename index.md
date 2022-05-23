# Confluence to Notion Converter

This open-source software project implements a tool that transforms XML data backup files from the Confluence cloud so that users can import the existing contents into Notion.

This project is still under development, and there is still a lot of work to be done to verify, implement, and test the functionality.

A lot of sharing and participation will help the project progress.

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
