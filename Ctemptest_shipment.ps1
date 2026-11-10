$body = @{
    trackingNumber = 'APX-20241107'
    weightKg = 2.5
    recipientName = 'John Doe'
    recipientPhone = '+15551234567'
    recipientAddress = '123 Main St'
    recipientCity = 'Springfield'
}
$json = $body | ConvertTo-Json -Depth 3
Write-Host "JSON: $json"
$response = Invoke-RestMethod -Uri 'http://127.0.0.1:54403/api/v2/shipments' -Method Post -ContentType 'application/json' -Body $body
Write-Host "Response: $($response | ConvertTo-Json -Depth 3)"
