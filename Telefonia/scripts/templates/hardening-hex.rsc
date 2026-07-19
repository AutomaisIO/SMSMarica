# Blindagem padrao SMS Marica - id=1 Ambulatorio Pericles (hEX, portas fixas)
/interface ethernet set ether1 comment="Prefeitura/ONU - PRIMARIO (seguro)"
/interface ethernet set ether2 comment="CONNECT - internet failover - NAO CONFIAVEL"
/interface ethernet set ether3 comment="gestao OOB (futuro: internet reserva)"
/interface ethernet set ether4 comment="LAN unidade - uplink switch core"
/interface ethernet set ether5 comment="circuito Wi-Fi"
/interface list add name=MGMT comment="interfaces de gestao"
/interface list member add list=MGMT interface=bridge-gestao
/interface list member add list=MGMT interface=automais-vpn
/system scheduler add name=SAFETY-revert interval=5m on-event="/ip firewall filter disable [find comment~\"BLINDAGEM Connect\"]; /ip ssh set strong-crypto=no; /ip service set ssh address=\"\"; /ip service set winbox address=\"\""
/ip dns set servers=8.8.8.8,9.9.9.9
/ip firewall filter add chain=input action=accept connection-state=established,related,untracked comment="input estado"
/ip firewall filter add chain=input action=drop connection-state=invalid comment="input invalid"
/ip firewall filter add chain=input action=accept in-interface=ether2 protocol=udp dst-port=51820 src-address=192.241.153.121 comment="excecao SERVER VOIP"
/ip firewall filter add chain=input action=accept in-interface=ether2 protocol=udp dst-port=51820,13324 comment="WireGuard VPNs"
/ip firewall filter add chain=input action=accept in-interface=ether2 protocol=udp dst-port=68 comment="dhcp-client Connect"
/ip firewall filter add chain=input action=drop in-interface=ether2 comment="BLINDAGEM Connect (bloqueia wifi/ISP)"
/ip service disable ftp,telnet,www,api-ssl
/ip service set ssh address=173.20.20.0/24,10.35.0.0/24
/ip service set winbox address=173.20.20.0/24,10.35.0.0/24
/tool mac-server set allowed-interface-list=MGMT
/tool mac-server mac-winbox set allowed-interface-list=MGMT
/tool mac-server ping set enabled=no
/ip neighbor discovery-settings set discover-interface-list=MGMT
/tool bandwidth-server set enabled=no
/ip ssh set strong-crypto=yes
