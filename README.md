# TextScanX

**TextScanX** is an open-source, cross-platform tool built with C# and powered by the Tesseract engine. It allows users to efficiently scan directories for image files, extract text from them, and store the results in a local SQLite database for quick retrieval. The tool supports multiple image formats and provides options for both normal and recursive directory scanning.

## Features

- Extracts text from image files (`.png`, `.jpg`, `.jpeg`, `.bmp`, `.webp`, `.tiff`) using Tesseract OCR.
- Supports normal and recursive directory searches.
- Saves extracted text and image metadata (such as hash and creation date) into a SQLite database.
- Allows for searching stored text from the database.
- Configurable confidence threshold for OCR results (default: 50%).

## How to Use

1. Run the application.
2. Select from the main menu:
   - **[1] Search Files**: Browse directories to scan images for text.
   - **[2] Search for Text**: Query the database for previously extracted text.
3. For directory searches, choose between:
   - **Normal Directory Search**: Scan only the specified directory.
   - **Recursive Directory Search**: Scan the directory and all its subdirectories.
4. Extracted text is stored in a local SQLite database, making it easy to search later.

## Configuration

- The tool uses a `config.json` file to set the mean confidence for OCR results. The default confidence level is 0.5 (50%). You can modify this value to suit your needs.

## Dependencies

- [Tesseract OCR](https://github.com/tesseract-ocr/tesseract) & [.NET Tesseract Library](https://github.com/charlesw/tesseract)
- [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite)
- [Newtonsoft.Json](https://www.newtonsoft.com/json)

## Installation
## Running Compiled Binary
1. Download from [Releases](https://github.com/whoswhip/TextScanX/releases/latest)
2. Extract files.
3. Run the application
## Building from Source
1. Clone this repository.
2. Install the required dependencies listed above.
3. Run the project using your preferred C# IDE or `dotnet run` from the terminal.

## License

This project is licensed under the [MIT License](https://github.com/whoswhip/TextScanX/blob/master/LICENSE.txt).
