import { useMemo, useState } from 'react';
import { Send } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { useConfiguracaoConfirmacao, useDispararLote, usePreviaLote, useRegrasUnidades } from '@/features/mensageria/api/queries';
import { diaLegivel, hojeMais } from '@/features/mensageria/lib/rotulos';
import type { DisparoLote, FiltroLote } from '@/features/mensageria/types';

function Numero({ rotulo, valor, destaque, dica }: { rotulo: string; valor: number; destaque?: string; dica?: string }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white px-4 py-3" title={dica}>
      <div className={`text-2xl font-semibold tabular-nums ${destaque ?? 'text-gray-900'}`}>{valor.toLocaleString('pt-BR')}</div>
      <div className="text-xs text-gray-500">{rotulo}</div>
    </div>
  );
}

function Opcao({
  checked, onChange, rotulo, dica, disabled,
}: { checked: boolean; onChange: (v: boolean) => void; rotulo: string; dica: string; disabled?: boolean }) {
  return (
    <label className="inline-flex items-center gap-1.5 text-sm text-gray-700" title={dica}>
      <input type="checkbox" checked={checked} disabled={disabled} onChange={(e) => onChange(e.target.checked)} />
      <span>{rotulo}</span>
    </label>
  );
}

/**
 * Disparo manual (tela de admin): avisa o estoque que a importação deixou para trás e, quando
 * pedido, reenvia para quem já recebeu ou já confirmou. Escolhe-se o período da AGENDA (não do
 * envio). As opções ficam discretas de propósito — quem chega aqui sabe o que está fazendo.
 */
export function AbaLote() {
  const podeEditar = usePermissao('Confirmacoes', 'Edicao') || usePermissao('NotificacoesAgendamento', 'Edicao');
  const regras = useRegrasUnidades();
  const cfg = useConfiguracaoConfirmacao();

  const [unidadeId, setUnidadeId] = useState('');
  const [de, setDe] = useState(hojeMais(1));
  const [ate, setAte] = useState(hojeMais(10));
  const [forcar, setForcar] = useState(false);
  const [ignorarJanela, setIgnorarJanela] = useState(false);
  const [incluirJaAvisados, setIncluirJaAvisados] = useState(false);
  const [incluirJaConfirmados, setIncluirJaConfirmados] = useState(false);
  const [confirmando, setConfirmando] = useState(false);

  // Forçando, a lista precisa ter TODAS as unidades: a graça é disparar para uma que está com a
  // chave desligada (sem religar o automático).
  const unidadesDaLista = useMemo(
    () => (regras.data ?? []).filter((u) => forcar || u.enviarConfirmacao),
    [regras.data, forcar],
  );

  const filtro: FiltroLote = {
    unidadeId: unidadeId || undefined,
    de,
    ate,
    forcar: forcar || undefined,
    incluirJaAvisados: incluirJaAvisados || undefined,
    incluirJaConfirmados: incluirJaConfirmados || undefined,
  };
  const disparo: DisparoLote = { ...filtro, ignorarJanela: ignorarJanela || undefined };
  const previa = usePreviaLote(filtro, Boolean(de && ate) && (!forcar || Boolean(unidadeId)));
  const disparar = useDispararLote();
  const p = previa.data;
  const resultado = disparar.data;

  const minutos = p ? Math.max(1, Math.ceil(p.elegiveis / 100)) : 0;
  const janela = cfg.data ? `${cfg.data.horaInicioEnvio}–${cfg.data.horaFimEnvio}` : 'horário de envio';

  return (
    <div className="space-y-4">
      <p className="text-sm text-gray-600">
        O aviso nasce na importação; ligar a chave da unidade não avisa quem já estava agendado. Este disparo cobre
        esse estoque com a mesma régua (só SISREG, unidade e procedimento ligados, agendamento futuro, sem resposta).
        As opções abaixo afrouxam a régua por decisão de quem clica.
      </p>

      <div className="grid grid-cols-1 gap-3 md:grid-cols-4">
        <label className="flex flex-col gap-1 text-sm md:col-span-2">
          <span className="font-medium text-gray-700">Unidade</span>
          <Select value={unidadeId} onChange={(e) => setUnidadeId(e.target.value)}>
            <option value="">Todas as unidades com aviso ligado</option>
            {unidadesDaLista.map((u) => (
              <option key={u.unidadeId} value={u.unidadeId}>{u.unidadeNome}</option>
            ))}
          </Select>
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Agenda de</span>
          <Input type="date" value={de} onChange={(e) => setDe(e.target.value)} />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">até</span>
          <Input type="date" value={ate} onChange={(e) => setAte(e.target.value)} />
        </label>
      </div>

      {podeEditar ? (
        <div className="flex flex-wrap items-center gap-x-5 gap-y-2 rounded-md border border-gray-200 bg-gray-50 px-3 py-2">
          <span className="text-xs font-medium uppercase tracking-wide text-gray-500">Opções</span>
          <Opcao
            checked={forcar}
            onChange={(v) => { setForcar(v); if (v) setUnidadeId(''); }}
            rotulo="Ignorar chaves de unidade/procedimento"
            dica="Exige escolher a unidade. Serve para avisar uma unidade com o automático desligado, sem religá-lo. Número negado continua sem receber; não verificado recebe o desafio primeiro."
          />
          <Opcao
            checked={ignorarJanela}
            onChange={setIgnorarJanela}
            rotulo={`Enviar agora (fora de ${janela})`}
            dica="Vale só para este lote; a janela continua valendo para o resto."
          />
          <Opcao
            checked={incluirJaAvisados}
            onChange={setIncluirJaAvisados}
            rotulo="Reenviar para quem já recebeu"
            dica="A comunicação é rearmada: links antigos revogados, recibos zerados, mensagem nova."
          />
          <Opcao
            checked={incluirJaConfirmados}
            onChange={setIncluirJaConfirmados}
            rotulo="Reenviar para quem já confirmou"
            dica="A resposta anterior fica na trilha e volta a 'sem resposta' — o paciente reconfirma."
          />
        </div>
      ) : null}

      {forcar && !unidadeId ? <p className="text-sm text-amber-800">Escolha a unidade para poder ignorar as chaves.</p> : null}
      {previa.isError ? <p className="text-sm text-red-700">{extrairMensagemDeErro(previa.error)}</p> : null}

      {p ? (
        <>
          <div className="grid grid-cols-2 gap-3 md:grid-cols-6">
            <Numero rotulo="Serão avisados" valor={p.elegiveis} destaque="text-emerald-700" />
            <Numero rotulo="Reenvios (já recebeu)" valor={p.reenviosAvisados} destaque={p.reenviosAvisados ? 'text-amber-700' : undefined} />
            <Numero rotulo="Reenvios (já confirmou)" valor={p.reenviosConfirmados} destaque={p.reenviosConfirmados ? 'text-amber-700' : undefined} />
            <Numero
              rotulo={forcar ? 'Proced. desligado (incluídos)' : 'Procedimento desligado'}
              valor={p.foraProcedimentoDesligado}
              dica={forcar ? 'No modo forçado a chave do procedimento é ignorada — estes entram no lote.' : 'Agendamentos cujo procedimento não está marcado para avisar.'}
            />
            <Numero rotulo="Já avisados (fora)" valor={p.foraJaAvisado} />
            <Numero rotulo="Fora do SISREG" valor={p.foraNaoSisreg} />
          </div>

          <div className="overflow-hidden rounded-lg border border-gray-200">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 text-xs uppercase text-gray-500">
                <tr>
                  <th className="px-3 py-2 text-left">Dia da agenda</th>
                  <th className="px-3 py-2 text-right">Total</th>
                  <th className="px-3 py-2 text-right">Consultas</th>
                  <th className="px-3 py-2 text-right">Exames</th>
                </tr>
              </thead>
              <tbody>
                {p.porDia.map((d) => (
                  <tr key={d.dia} className="border-t border-gray-100">
                    <td className="px-3 py-1.5">{diaLegivel(d.dia)}</td>
                    <td className="px-3 py-1.5 text-right tabular-nums font-medium">{d.total}</td>
                    <td className="px-3 py-1.5 text-right tabular-nums text-gray-600">{d.consultas}</td>
                    <td className="px-3 py-1.5 text-right tabular-nums text-gray-600">{d.exames}</td>
                  </tr>
                ))}
                {p.porDia.length === 0 ? (
                  <tr><td colSpan={4} className="px-3 py-3 text-center text-gray-500">Nada a avisar neste período.</td></tr>
                ) : null}
              </tbody>
            </table>
          </div>

          {p.aviso ? <p className="text-sm text-amber-800">{p.aviso}</p> : null}

          {p.elegiveis > 0 ? (
            <p className="text-xs text-gray-500">
              Entram na fila e saem no ritmo do worker (cerca de {minutos} min).{' '}
              {ignorarJanela ? 'Começam a sair na próxima rodada.' : `Dentro de ${janela}; fora dela, esperam a janela abrir.`}
            </p>
          ) : null}
        </>
      ) : null}

      {resultado ? (
        <p className="text-sm text-emerald-800">{resultado.enfileiradas} mensagem(ns) na fila — acompanhe em <strong>Envios</strong>.</p>
      ) : null}
      {disparar.isError ? <p className="text-sm text-red-700">{extrairMensagemDeErro(disparar.error)}</p> : null}

      {podeEditar && p && p.elegiveis > 0 ? (
        <div className="flex items-center gap-2">
          {confirmando ? (
            <>
              <Button
                variante="danger"
                tamanho="sm"
                disabled={disparar.isPending}
                onClick={() => { disparar.mutate(disparo); setConfirmando(false); }}
              >
                Sim, disparar {p.elegiveis}{ignorarJanela ? ' agora' : ''}
              </Button>
              <Button variante="outline" tamanho="sm" onClick={() => setConfirmando(false)}>Cancelar</Button>
              <span className="text-xs text-gray-500">
                Mensagens reais para pacientes; a resposta volta para a Central e para Confirmações.
              </span>
            </>
          ) : (
            <Button tamanho="sm" onClick={() => setConfirmando(true)} disabled={disparar.isPending}>
              <Send className="mr-1.5 h-4 w-4" /> Disparar lote
            </Button>
          )}
        </div>
      ) : null}
    </div>
  );
}
