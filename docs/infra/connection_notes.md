# Connect to SQL Server
# Note: the prod compose stack does not publish 1433 on the VPS host. To use an SSH tunnel, first bind SQL Server to VPS loopback (e.g., `ports: ["127.0.0.1:1433:1433"]` under `sqlserver`), then:
ssh -L 1433:127.0.0.1:1433 deploy@<vps-ip>   # then point SSMS/Rider at localhost:1433