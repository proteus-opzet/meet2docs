# Meet2Docs

Extract user availability from When2Meet data.

## Quick Start

You can run the GUI version by simply double-clicking `meet2Docs-gui-0.3.0-win-x64.exe` on Windows, `meet2Docs-gui-0.3.0-linux-x64` on Linux, or `meet2Docs-gui-0.3.0-osx-x64` on macOS.

### Alternatively, run via Command-line:

* Linux
	```bash
	./meet2Docs-cli-0.3.0-linux-x64 --urls=https://when2meet.com/ChangeMe1,https://when2meet.com/ChangeMe2 --select-only="Name Surname 1","Name Surname 2"
	```
* macOS
	```bash
	./meet2Docs-cli-0.3.0-osx-x64 --urls=https://when2meet.com/ChangeMe1,https://when2meet.com/ChangeMe2 --select-only="Name Surname 1","Name Surname 2"
	```
* Windows
	```bat
	.\meet2Docs-cli-0.3.0-win-x64.exe --urls=https://when2meet.com/ChangeMe1,https://when2meet.com/ChangeMe2 --select-only="Name Surname 1","Name Surname 2"
	```

* You can also build it from source:

1. Clone the repo and enter it:

   ```bash
   git clone https://github.com/proteus-opzet/meet2docs.git
   cd ./meet2docs/Meet2Docs.Cli
   ```

2. Run the project. Example:

   ```bash
   dotnet run -- --urls=https://when2meet.com/ChangeMe1,https://when2meet.com/ChangeMe2 --select-only="Name Surname 1","Name Surname 2"
   ```

##  Development, debugging

* Building requires [.NET 9 SDK](https://dotnet.microsoft.com/)
* You can use for example Visual Studio, VSCode or VSCodium
