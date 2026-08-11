寫好可用 powershell 註冊成 Windows Service 
sc.exe create ArchiveProcessor binPath= "{YourPath}" start= auto

將來若要停止服務
sc.exe stop ArchiveProcessor

刪除服務
sc.exe delete ArchiveProcessor