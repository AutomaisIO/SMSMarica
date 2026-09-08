# FO-tick v5 (Motor B) - PERICLES id=1
# Estado = flag disabled da regra bridge nat "FO-CAPTURA". Sem estado em RAM.
# Normal    : FO-* off, SHIM on, redirect DNS off  -> ponte transparente (PMM dona)
# Contingencia: FO-* on, SHIM off, redirect DNS on -> captura L2 + saida por wg-eveo
#
# 08/09/2026 - duas correcoes (ver docs/incidente-pericles-080926.md):
#   1) removido o bloco authoritative/delay-threshold: essas propriedades NAO
#      existem mais em /ip dhcp-server no RouterOS 7.23.2 (get devolve vazio),
#      a comparacao != "yes" nunca fechava e o tick reescrevia o servidor DHCP
#      a CADA 5s -> log do MK reduzido a 4 min e ~1,2 setor/s de escrita em flash.
#   2) o foreach das regras de redirect DNS passou de comment~"FO dns udp" para
#      comment~"FO dns", para cobrir tambem a regra TCP (F3 preve UDP e TCP).
#      O bloco duplicado que existia foi removido.
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
:do { :set linkUp [/interface get [find name=ether1] running] } on-error={}
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
# redirect DNS no nivel IP: UDP e TCP (comment "FO dns udp" / "FO dns tcp")
:foreach r in=[/ip firewall nat find comment~"FO dns"] do={
  :do { :if ([/ip firewall nat get $r disabled] = $want) do={ /ip firewall nat set $r disabled=(!$want) } } on-error={ :set err ($err + 1) }
}
# --- rota default pelo tunel DC: so em contingencia ---
:foreach r in=[/ip route find comment~"FO default via EVEO"] do={
  :do { :if ([/ip route get $r disabled] = $want) do={ /ip route set $r disabled=(!$want) } } on-error={ :set err ($err + 1) }
}
# --- DHCP do MK = espelho do estado do failover (decisao 02/09) ---
# Contingencia: liga. Normal: desliga (PMM e a dona).
# NAO tocar em authoritative/delay-threshold: nao existem no 7.23.2 e a
# tentativa de reconcilia-las gravava a config a cada tick.
:foreach d in=[/ip dhcp-server find name="FAILOVER-dhcp"] do={
  :do {
    :if ([/ip dhcp-server get $d disabled] = $want) do={ /ip dhcp-server set $d disabled=(!$want) }
  } on-error={ :set err ($err + 1) }
}
# --- transicao ---
:if ($want != $emFO) do={
  :foreach c in=[/ip firewall connection find src-address~"10.1.19"] do={ :do { /ip firewall connection remove $c } on-error={} }
  :do { /ip dns cache flush } on-error={}
  :set foDown 0; :set foUp 0
  :log warning ("FO-v5 " . [:tostr $want] . " pmmUp=" . $pmmUp . "/3 link=" . [:tostr $linkUp] . " err=" . $err)
}
