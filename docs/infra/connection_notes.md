# connect to the sql server
ssh -L 1433:127.0.0.1:1433 deploy@<vps-ip>   # then point SSMS/Rider at localhost:1433