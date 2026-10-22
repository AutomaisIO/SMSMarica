import { useState } from 'react';
import { BadgeCheck, CheckCircle2, Merge, X } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { Button } from '@/shared/ui/Button';
import { Passos } from '@/features/manual/components/Passos';
import { TelaSimulada } from '@/features/manual/components/TelaSimulada';
import { CelulaUnificacao } from '@/features/pacientes/components/UnificarPacientesModal';

/**
 * Simulação da unificação: a lista em modo "Unificar paciente", a marcação dos dois cadastros e
 * o modal de escolha dado a dado. Usa a MESMA célula da tela real (CelulaUnificacao). Dados
 * inventados, CPF inválido, nada sai do navegador.
 */

type Lado = 'a' | 'b';
type LinhaUm = { chave: string; rotulo: string; a: string; b: string };
type ItemSim = { chave: string; texto: string; emA: boolean; emB: boolean; verificado?: boolean };
type LinhaItens = { chave: string; rotulo: string; itens: ItemSim[] };

const CADASTROS = [
  { id: 'a', nome: 'JOANA RIBEIRO (exemplo)', cpf: '000.000.001-00', nasc: '12/03/1980' },
  { id: 'b', nome: 'JOANA RIBEIRO DA SILVA (exemplo)', cpf: '', nasc: '12/03/1980' },
  { id: 'c', nome: 'CARLOS MENDES (exemplo)', cpf: '000.000.002-00', nasc: '30/07/1965' },
];

const LINHAS_UM: LinhaUm[] = [
  { chave: 'nome', rotulo: 'Nome', a: 'JOANA RIBEIRO (exemplo)', b: 'JOANA RIBEIRO DA SILVA (exemplo)' },
  { chave: 'cpf', rotulo: 'CPF', a: '000.000.001-00', b: '' },
  { chave: 'nasc', rotulo: 'Nascimento', a: '12/03/1980', b: '12/03/1980' },
  { chave: 'mae', rotulo: 'Mãe', a: '', b: 'RITA RIBEIRO (exemplo)' },
  { chave: 'end', rotulo: 'Endereço', a: 'Rua das Flores, 10, Centro', b: 'Rua das Flores, 100, Centro' },
];

const LINHAS_ITENS: LinhaItens[] = [
  {
    chave: 'tel', rotulo: 'Telefones', itens: [
      { chave: '1', texto: '(21) 90000-0001', emA: true, emB: true },
      { chave: '2', texto: '(21) 2600-0000', emA: true, emB: false },
      { chave: '3', texto: '(21) 90000-0002', emA: false, emB: true, verificado: true },
    ],
  },
  {
    chave: 'alergias', rotulo: 'Alergias', itens: [
      { chave: 'dipirona', texto: 'Dipirona', emA: true, emB: true },
      { chave: 'latex', texto: 'Látex', emA: true, emB: false },
      { chave: 'penicilina', texto: 'Penicilina', emA: false, emB: true },
    ],
  },
];

const PASSOS = [
  { titulo: 'Marque o primeiro cadastro', detalhe: 'Ele fica esmaecido e guardado — dá para buscar de novo.' },
  { titulo: 'Marque o outro cadastro da mesma pessoa', detalhe: 'O modal abre com os dois lado a lado.' },
  { titulo: 'Clique nos dados que devem ficar e em Aplicar', detalhe: 'Verde fica; esmaecido não fica.' },
];

export function SimulacaoUnificarPaciente() {
  const [primeiro, setPrimeiro] = useState<string | null>(null);
  const [segundo, setSegundo] = useState<string | null>(null);
  const [toques, setToques] = useState<Record<string, Lado>>({});
  const [desligados, setDesligados] = useState<Set<string>>(() => new Set());
  const [aplicado, setAplicado] = useState(false);

  function reiniciar() {
    setPrimeiro(null);
    setSegundo(null);
    setToques({});
    setDesligados(new Set());
    setAplicado(false);
  }

  function marcar(id: string) {
    if (primeiro === id) setPrimeiro(null);
    else if (!primeiro) setPrimeiro(id);
    else setSegundo(id);
  }

  function ladoQueFica(l: LinhaUm): Lado {
    if (toques[l.chave]) return toques[l.chave];
    if (!l.a) return 'b';
    return 'a';
  }

  const ligado = (l: LinhaItens, i: ItemSim) => i.verificado || !desligados.has(`${l.chave}:${i.chave}`);
  function alternar(l: LinhaItens, i: ItemSim) {
    if (i.verificado) return;
    if (l.chave === 'tel' && ligado(l, i) && l.itens.filter((x) => ligado(l, x)).length === 1) return;
    const k = `${l.chave}:${i.chave}`;
    setDesligados((s) => {
      const novo = new Set(s);
      if (novo.has(k)) novo.delete(k);
      else novo.add(k);
      return novo;
    });
  }

  // A simulação só abre o modal para os dois cadastros da Joana (o terceiro é outra pessoa).
  const par = primeiro && segundo ? [primeiro, segundo].sort().join('') : '';
  const passo = aplicado ? 3 : segundo ? 2 : primeiro ? 1 : 0;
  const telefonesDesligados = [...desligados].filter((k) => k.startsWith('tel:')).length;

  return (
    <TelaSimulada aoReiniciar={reiniciar}>
      <div className="space-y-4">
        <Passos itens={PASSOS} atual={Math.min(passo, 2)} concluidos={passo} />

        <div className="flex items-center justify-between gap-3">
          <p className="text-sm font-semibold text-gray-900">Pacientes</p>
          <Button variante="outline" tamanho="sm" onClick={reiniciar}>
            <X className="h-4 w-4" /> Cancelar unificação
          </Button>
        </div>

        <div className="rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-900">
          {primeiro
            ? <>Marcado: <strong>{CADASTROS.find((c) => c.id === primeiro)?.nome}</strong>. Agora marque o outro cadastro da mesma pessoa.</>
            : 'Marque os dois cadastros da mesma pessoa.'}
        </div>

        <div className="overflow-hidden rounded-lg border border-gray-200 bg-white">
          {CADASTROS.map((c) => {
            const marcado = primeiro === c.id || segundo === c.id;
            return (
              <button
                key={c.id}
                type="button"
                onClick={() => marcar(c.id)}
                className={cn(
                  'flex w-full items-center gap-3 border-b border-gray-100 px-3 py-2 text-left text-sm last:border-b-0 hover:bg-gray-50',
                  primeiro === c.id && 'opacity-50',
                )}
              >
                <input type="checkbox" readOnly checked={marcado} tabIndex={-1} className="h-4 w-4 accent-green-600" />
                <span className="flex-1 font-medium text-gray-900">{c.nome}</span>
                <span className="w-32 text-gray-600">{c.cpf || 'sem CPF'}</span>
                <span className="w-24 text-gray-600">{c.nasc}</span>
              </button>
            );
          })}
        </div>

        {segundo && par !== 'ab' ? (
          <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
            Na simulação, o modal abre para os dois cadastros da Joana. O Carlos é outra pessoa —{' '}
            <button type="button" className="underline" onClick={() => setSegundo(null)}>desmarque</button>.
          </div>
        ) : null}

        {par === 'ab' ? (
          <div className="rounded-xl border border-gray-300 bg-white shadow-lg">
            <div className="border-b border-gray-200 px-5 py-3">
              <p className="flex items-center gap-2 text-base font-semibold text-gray-900">
                <Merge className="h-4 w-4" /> Unificar cadastros
              </p>
              <p className="text-sm text-gray-500">
                Clique no dado que deve ficar. O que está em verde vai para o cadastro unificado.
              </p>
            </div>
            {aplicado ? (
              <div className="space-y-2 p-6 text-center">
                <CheckCircle2 className="mx-auto h-9 w-9 text-green-600" />
                <p className="font-semibold text-gray-900">Cadastros unificados</p>
                <p className="text-sm text-gray-700">
                  <strong>3</strong> registros do painel e <strong>5</strong> do histórico clínico vieram
                  junto.
                  {telefonesDesligados > 0
                    ? <> <strong>{telefonesDesligados}</strong> telefone(s) desligado(s) ficaram só no histórico.</>
                    : null}
                </p>
                <p className="text-xs text-gray-500">
                  O outro cadastro foi desativado e aponta para este.
                </p>
              </div>
            ) : (
              <div className="space-y-4 p-5">
                <div className="grid grid-cols-[6.5rem_minmax(0,1fr)_minmax(0,1fr)] items-start gap-x-3 gap-y-2">
                  {LINHAS_UM.map((l) => (
                    <div key={l.chave} className="contents">
                      <span className="pt-2.5 text-xs text-gray-500">{l.rotulo}</span>
                      {l.a === l.b ? (
                        <CelulaUnificacao texto={l.a} escolhido clicavel={false} largo />
                      ) : (
                        (['a', 'b'] as Lado[]).map((lado) => (
                          <CelulaUnificacao
                            key={lado}
                            texto={l[lado]}
                            escolhido={ladoQueFica(l) === lado}
                            clicavel={l[lado] !== ''}
                            aoClicar={() => setToques((t) => ({ ...t, [l.chave]: lado }))}
                          />
                        ))
                      )}
                    </div>
                  ))}
                  {LINHAS_ITENS.map((l) => l.itens.map((i, n) => {
                    const celula = (
                      <CelulaUnificacao
                        texto={i.texto}
                        escolhido={ligado(l, i)}
                        clicavel={!i.verificado}
                        aoClicar={() => alternar(l, i)}
                        largo={i.emA && i.emB}
                        selo={i.verificado ? (
                          <span className="inline-flex shrink-0 items-center gap-1 rounded bg-green-50 px-1.5 py-0.5 text-[11px] font-medium text-green-800">
                            <BadgeCheck className="h-3.5 w-3.5" /> verificado
                          </span>
                        ) : null}
                      />
                    );
                    return (
                      <div key={`${l.chave}:${i.chave}`} className="contents">
                        <span className="pt-2.5 text-xs text-gray-500">{n === 0 ? l.rotulo : ''}</span>
                        {i.emA && i.emB ? celula : (
                          <>
                            {i.emA ? celula : <div />}
                            {i.emB ? celula : <div />}
                          </>
                        )}
                      </div>
                    );
                  }))}
                </div>
                <p className="border-t border-gray-200 pt-3 text-sm text-gray-600">
                  Vem junto: Solicitações e agendamentos (SISREG) <strong>2</strong> · Laudos{' '}
                  <strong>1</strong> — e todo o histórico clínico.
                </p>
                <div className="flex justify-end gap-3">
                  <Button variante="outline" onClick={() => setSegundo(null)}>Cancelar</Button>
                  <Button onClick={() => setAplicado(true)}>Aplicar</Button>
                </div>
              </div>
            )}
          </div>
        ) : null}
      </div>
    </TelaSimulada>
  );
}
