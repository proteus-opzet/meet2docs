# Meet2Docs

Extract user availability from When2Meet data.

## Quick Start

* Linux
	```bash
	./meet2docs --urls=https://when2meet.com/ChangeMe1,https://when2meet.com/ChangeMe2 --select-only="Name Surname 1","Name Surname 2"
	```
* Windows
	```bat
	./meet2docs.exe --urls=https://when2meet.com/ChangeMe1,https://when2meet.com/ChangeMe2 --select-only="Name Surname 1","Name Surname 2"
	```

* You can also build it from source:

1. Clone the repo and enter it:

   ```bash
   git clone https://github.com/proteus-opzet/meet2docs.git
   cd ./meet2docs
   ```

2. Run the project. Example:

   ```bash
   dotnet run -- --urls=https://when2meet.com/ChangeMe1,https://when2meet.com/ChangeMe2 --select-only="Name Surname 1","Name Surname 2"
   ```

##  Development, debugging

* Building requires [.NET 9 SDK](https://dotnet.microsoft.com/)
* You can use for example Visual Studio, VSCode or VSCodium
