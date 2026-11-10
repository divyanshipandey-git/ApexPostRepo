$c = New-Object Net.Sockets.TcpClient; $c.Connect("127.0.0.1", 54403); Write-Host "Connected: $($c.Connected)"; $c.Close()
