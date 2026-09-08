# FO-harvest - grava o IP atual de cada host como lease estatica
:local n 0
:local j 0
:foreach a in=[/ip arp find interface=bridge-transparente] do={
  :local ip [/ip arp get $a address]
  :local mac [/ip arp get $a mac-address]
  :if ([:len $mac] > 0) do={
    :if ([:typeof [:find $ip "10.1.19."]] != "nil") do={
      :if ((($ip != "10.1.19.1") and ($ip != "10.1.19.254")) and ([:len [/ip dhcp-server lease find address=$ip]] = 0)) do={
        :do {
          /ip dhcp-server lease add server=FAILOVER-dhcp address=$ip mac-address=$mac comment="harvest 030926"
          :set n ($n + 1)
        } on-error={ :set j ($j + 1) }
      }
    }
  }
}
:log warning ("FO-harvest: criadas=" . $n . " falhas=" . $j)
:put ("criadas=" . $n . " falhas=" . $j)
