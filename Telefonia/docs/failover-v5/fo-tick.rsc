# FO-tick v5 (Motor B) - Complexo Regulador id=0
# Estado = flag disabled da regra bridge nat "FO-CAPTURA". Sem estado em RAM.
# Normal    : FO-* off, SHIM on, redirect DNS off  -> ponte transparente (PMM dona)
# Contingencia: FO-* on, SHIM off, redirect DNS on -> captura L2 + saida por wg-eveo
:global foDown; :global foUp
:if ([:typeof $foDown] != "num") do={ :set foDown 0 }
:if ([:typeof $foUp] != "num") do={ :set foUp 0 }
:local upS ([:tonsec [/system resource get uptime]] / 1000000000)
:if ($upS < 180) do={ :log info "FO: graca pos-boot"; :error "graca" }
:local anc [/interface bridge nat find comment="FO-CAPTURA"]
:if ([:len $anc] = 0) do={ :log error "FO: ancora ausente"; :error "sem ancora" }
:local emFO (![/interface bridge nat get $anc disabled])
# --- sondas ---
:local pmmUp 0
:local pmmDown 0
:foreach n in=[/tool netwatch find comment~"FO-PMM"] do={
  :local st [/tool netwatch get $n status]
  :if ($st = "up") do={ :set pmmUp ($pmmUp + 1) }
  :if ($st = "down") do={ :set pmmDown ($pmmDown + 1) }
}
:local linkUp false
:do { :set linkUp [/interface get [find name=combo1] running] } on-error={}
:local pmmOK (($pmmUp >= 2) && $linkUp)
:local pmmDOWN ((($pmmUp = 0) && ($pmmDown >= 2)) || (!$linkUp))
# --- debounce ---
:if ($pmmDOWN) do={ :set foDown ($foDown + 1) } else={ :set foDown 0 }
:if ($pmmOK)   do={ :set foUp   ($foUp + 1) }   else={ :set foUp 0 }
# --- decisao --- (entra: 6 ticks=30s ; SAI: 24 ticks=120s de sondas boas)
:local want $emFO
:if ($emFO) do={
  :if ($foUp >= 24) do={ :set want false }
} else={
  :local entrar (($foDown >= 6) || ((!$linkUp) && ($foDown >= 3)))
  :if ($entrar) do={ :set want true }
}
# --- reconciliacao idempotente (roda TODO tick) ---
:local err 0
:foreach r in=[/interface bridge nat find comment~"^FO-"] do={
  :do { :if ([/interface bridge nat get $r disabled] = $want) do={ /interface bridge nat set $r disabled=(!$want) } } on-error={ :set err ($err + 1) }
}
:foreach r in=[/interface bridge nat find comment~"SHIM retorno"] do={
  :do { :if ([/interface bridge nat get $r disabled] = (!$want)) do={ /interface bridge nat set $r disabled=$want } } on-error={ :set err ($err + 1) }
}
:foreach r in=[/ip firewall nat find comment~"FAILOVER dns"] do={
  :do { :if ([/ip firewall nat get $r disabled] = $want) do={ /ip firewall nat set $r disabled=(!$want) } } on-error={ :set err ($err + 1) }
}
# --- DHCP do MK = espelho do estado do failover (decisao 02/09) ---
# Contingencia: liga (authoritative=yes, responde na hora). Normal: desliga (PMM e a dona).
:foreach d in=[/ip dhcp-server find name="FAILOVER-dhcp"] do={
  :do {
    :if ([/ip dhcp-server get $d disabled] = $want) do={ /ip dhcp-server set $d disabled=(!$want) }
    :local aut [:tostr [/ip dhcp-server get $d authoritative]]
    :if ($want) do={
      :if ($aut != "yes") do={ /ip dhcp-server set $d authoritative=yes delay-threshold=0s }
    } else={
      :if ($aut != "no") do={ /ip dhcp-server set $d authoritative=no delay-threshold=5s }
    }
  } on-error={ :set err ($err + 1) }
}
# --- transicao ---
:if ($want != $emFO) do={
  :foreach c in=[/ip firewall connection find src-address~"10.3.74"] do={ :do { /ip firewall connection remove $c } on-error={} }
  :do { /ip dns cache flush } on-error={}
  :set foDown 0; :set foUp 0
  :log warning ("FO-v5 " . [:tostr $want] . " pmmUp=" . $pmmUp . "/3 link=" . [:tostr $linkUp] . " err=" . $err)
}
