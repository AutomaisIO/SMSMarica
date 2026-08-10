import { useMemo, useState } from 'react';
import { AlertTriangle, FilePlus2, Loader2, Radio, Send } from 'lucide-react';

import {
  useCamposNovaSer,
  useFormularioNovaSer,
  useRecursosNovaSer,
} from '@/features/ser/api/queries';
import type { CampoDinamicoSer, OpcaoSer } from '@/features/ser/types';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';

/**
 * Regulação → Nova solicitação: o formulário de criação do SER, montado AO VIVO.
 *
 * O SER divide o pedido em duas partes — um bloco fixo e um bloco DINÂMICO que muda conforme o
 * Recurso. Medido em 08/08/2026: 203 recursos produzem 21 formulários distintos, com 163 campos
 * únicos. Oncologia pede peso, altura, IMC e datas de biópsia; PET-CT pede grau histopatológico;
 * cardiologia pede NYHA e grupo sanguíneo. Por isso a tela não tem catálogo chumbado: ela
 * pergunta ao SER quais campos aquele recurso exige, e desenha o que vier.
 *
 * ENVIO DESLIGADO. Tudo aqui é leitura; o botão de enviar existe para deixar claro onde ele vai
 * ficar, mas está bloqueado e a tela mostra exatamente o que SERIA enviado. Ligar é decisão
 * explícita — a trava de somente-leitura do motor continua barrando o "Gravar" do SER.
 */
export function SerNovaSolicitacaoPage() {
  const [tipo, setTipo] = useState<string>('');
  const [recurso, setRecurso] = useState<string>('');
  const [fixos, setFixos] = useState<Record<string, string>>({});
  const [dinamicos, setDinamicos] = useState<Record<string, string>>({});
  const [verPayload, setVerPayload] = useState(false);

  const form = useFormularioNovaSer(true);
  const recursos = useRecursosNovaSer(tipo || undefined);
  const campos = useCamposNovaSer(tipo || undefined, recurso || undefined);

  const listaCampos = campos.data ?? [];

  const faltando = useMemo(() => {
    const f: string[] = [];
    if (!tipo) f.push('Tipo');
    if (!recurso) f.push('Recurso');
    if (!fixos.numeroCADSUS?.trim()) f.push('CNS do paciente');
    if (!fixos.classificacao_risco) f.push('Classificação de risco');
    if (!fixos.procedimento?.trim()) f.push('Hipótese');
    for (const c of listaCampos) {
      if (c.obrigatorio && !dinamicos[c.campo]?.trim()) f.push(c.rotulo);
    }
    return f;
  }, [tipo, recurso, fixos, dinamicos, listaCampos]);

  /** Exatamente o que seria postado no `form0` do SER — o payload, não uma aproximação dele. */
  const payload = useMemo(
    () => ({
      'form0:comboSisReg': fixos.comboSisReg ?? '',
      'form0:comboTipoRecurso': tipo,
      'form0:comboRecurso': recurso,
      'form0:numeroCADSUS': fixos.numeroCADSUS ?? '',
      'form0:medicoResp': fixos.medicoResp ?? '',
      'form0:telefoneCelularMedico': fixos.telefoneCelularMedico ?? '',
      'form0:especialidadeMedico': fixos.especialidadeMedico ?? '',
      'form0:classificacao_risco': fixos.classificacao_risco ?? '',
      'form0:procedimento': fixos.procedimento ?? '',
      'form0:unidadeNaoIdentificada': fixos.unidadeNaoIdentificada ?? '',
      ...dinamicos,
    }),
    [tipo, recurso, fixos, dinamicos],
  );

  function trocarTipo(v: string) {
    setTipo(v);
    // Recurso e campos dinâmicos pertencem ao tipo anterior: manter seria montar um pedido
    // com campos que o novo recurso nem tem.
    setRecurso('');
    setDinamicos({});
  }

  return (
    <div className="space-y-4">
      <header className="flex items-center gap-3">
        <FilePlus2 className="size-6 text-red-700" />
        <div>
          <h1 className="text-xl font-semibold text-slate-900">Nova solicitação (SER)</h1>
          <p className="text-sm text-slate-600">
            Formulário montado ao vivo a partir do SER — os campos mudam conforme o recurso.
          </p>
        </div>
        <span
          className="ml-auto inline-flex items-center gap-1.5 rounded-full bg-blue-50 px-2.5 py-1 text-xs font-medium text-blue-800"
          title="A tela lê a aba Editar do SER para saber quais campos aquele recurso exige. Nada é criado."
        >
          <Radio className="size-3.5" />
          catálogo ao vivo
        </span>
      </header>

      <div className="flex items-start gap-2 rounded-lg border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
        <AlertTriangle className="mt-0.5 size-4 shrink-0" />
        <p>
          <strong>Envio desligado.</strong> A tela está completa e o pedido é montado de verdade,
          mas nada é criado no SER. Quando o envio for ligado, será decisão explícita — e o que
          vai ser postado você já consegue conferir aqui embaixo, campo a campo.
        </p>
      </div>

      {form.isLoading && (
        <div className="flex items-center gap-2 p-6 text-slate-500">
          <Loader2 className="size-4 animate-spin" /> lendo o formulário do SER…
        </div>
      )}

      {form.isError && (
        <div className="rounded-lg border border-red-300 bg-red-50 p-4 text-sm text-red-800">
          Não consegui ler o formulário do SER. Verifique a credencial em Regulação →
          Configuração.
        </div>
      )}

      {form.data && (
        <div className="space-y-5 rounded-lg border border-slate-200 bg-white p-4">
          <Secao titulo="O que está sendo pedido">
            <Campo label="Tipo *" htmlFor="ns-tipo" className="w-48">
              <Select id="ns-tipo" value={tipo} onChange={(e) => trocarTipo(e.target.value)}>
                <option value="">Selecione…</option>
                {form.data.tipos.map((o) => (
                  <option key={o.valor} value={o.valor}>{o.rotulo}</option>
                ))}
              </Select>
            </Campo>

            <Campo label="Recurso *" htmlFor="ns-recurso" className="min-w-96 flex-1">
              <Select
                id="ns-recurso"
                value={recurso}
                onChange={(e) => {
                  setRecurso(e.target.value);
                  setDinamicos({});
                }}
                disabled={!tipo || recursos.isLoading}
              >
                <option value="">
                  {recursos.isLoading ? 'carregando do SER…' : 'Selecione…'}
                </option>
                {(recursos.data ?? []).map((o) => (
                  <option key={o.valor} value={o.valor}>{o.rotulo}</option>
                ))}
              </Select>
            </Campo>

            {recursos.data && (
              <p className="w-full text-xs text-slate-500">
                {recursos.data.length} recursos disponíveis para {tipo}.
              </p>
            )}
          </Secao>

          <Secao titulo="Paciente e solicitante">
            <Campo label="CNS do paciente *" htmlFor="ns-cns" className="w-56">
              <Input
                id="ns-cns"
                value={fixos.numeroCADSUS ?? ''}
                onChange={(e) => setFixos({ ...fixos, numeroCADSUS: e.target.value })}
              />
            </Campo>

            <Campo label="Médico responsável" htmlFor="ns-medico" className="min-w-80 flex-1">
              <Select
                id="ns-medico"
                value={fixos.medicoResp ?? ''}
                onChange={(e) => setFixos({ ...fixos, medicoResp: e.target.value })}
              >
                <option value="">Selecione…</option>
                {form.data.medicos.map((o) => (
                  <option key={o.valor} value={o.valor}>{o.rotulo}</option>
                ))}
              </Select>
            </Campo>

            <Campo label="Telefone do médico" htmlFor="ns-tel" className="w-44">
              <Input
                id="ns-tel"
                value={fixos.telefoneCelularMedico ?? ''}
                onChange={(e) => setFixos({ ...fixos, telefoneCelularMedico: e.target.value })}
              />
            </Campo>

            <Campo label="Especialidade" htmlFor="ns-esp" className="w-52">
              <Input
                id="ns-esp"
                value={fixos.especialidadeMedico ?? ''}
                onChange={(e) => setFixos({ ...fixos, especialidadeMedico: e.target.value })}
              />
            </Campo>
          </Secao>

          <Secao titulo="Classificação">
            <Campo label="Classificação de risco *" htmlFor="ns-risco" className="w-56">
              <Select
                id="ns-risco"
                value={fixos.classificacao_risco ?? ''}
                onChange={(e) => setFixos({ ...fixos, classificacao_risco: e.target.value })}
              >
                <option value="">Selecione…</option>
                {form.data.classificacoesRisco.map((o) => (
                  <option key={o.valor} value={o.valor}>{o.rotulo}</option>
                ))}
              </Select>
            </Campo>

            <Campo label="Hipótese *" htmlFor="ns-hipotese" className="min-w-80 flex-1">
              <Input
                id="ns-hipotese"
                value={fixos.procedimento ?? ''}
                onChange={(e) => setFixos({ ...fixos, procedimento: e.target.value })}
              />
            </Campo>

            <Campo label="Unidade de origem" htmlFor="ns-unid" className="min-w-80 flex-1">
              <Input
                id="ns-unid"
                value={fixos.unidadeNaoIdentificada ?? ''}
                onChange={(e) => setFixos({ ...fixos, unidadeNaoIdentificada: e.target.value })}
              />
            </Campo>
          </Secao>

          {/* O coração da tela: o que o SER pede muda por recurso, então isto é desenhado a
              partir do que ele respondeu — não de uma lista nossa. */}
          <section>
            <h2 className="mb-1 font-semibold text-slate-800">Campos do recurso</h2>
            {!recurso && (
              <p className="text-sm text-slate-500">
                Escolha o recurso para o SER dizer quais campos ele exige.
              </p>
            )}
            {campos.isLoading && (
              <p className="flex items-center gap-2 text-sm text-slate-500">
                <Loader2 className="size-4 animate-spin" /> perguntando ao SER…
              </p>
            )}
            {recurso && !campos.isLoading && (
              <>
                <p className="mb-3 text-xs text-slate-500">
                  {listaCampos.length} campo(s) — {listaCampos.filter((c) => c.obrigatorio).length}{' '}
                  obrigatório(s).
                </p>
                <div className="flex flex-wrap gap-3">
                  {listaCampos.map((c) => (
                    <CampoDinamico
                      key={c.campo}
                      c={c}
                      valor={dinamicos[c.campo] ?? ''}
                      onChange={(v) => setDinamicos({ ...dinamicos, [c.campo]: v })}
                    />
                  ))}
                </div>
              </>
            )}
          </section>

          <section className="border-t border-slate-200 pt-4">
            {faltando.length > 0 && (
              <p className="mb-2 text-sm text-amber-700">
                Faltam {faltando.length} campo(s) obrigatório(s): {faltando.slice(0, 6).join(', ')}
                {faltando.length > 6 && ' …'}
              </p>
            )}

            <div className="flex flex-wrap items-center gap-2">
              <Button disabled title="O envio ao SER ainda não está habilitado">
                <Send className="size-4" />
                Enviar ao SER (desligado)
              </Button>
              <Button variante="secundaria" onClick={() => setVerPayload((v) => !v)}>
                {verPayload ? 'Ocultar' : 'Ver'} o que seria enviado
              </Button>
            </div>

            {verPayload && (
              // Mostrar o payload é o que torna "preparada" verificável: dá para conferir nome
              // por nome contra a tela do SER antes de ligar o envio.
              <pre className="mt-3 max-h-80 overflow-auto rounded bg-slate-900 p-3 text-xs text-slate-100">
                {JSON.stringify(payload, null, 2)}
              </pre>
            )}
          </section>
        </div>
      )}
    </div>
  );
}

function Secao({ titulo, children }: { titulo: string; children: React.ReactNode }) {
  return (
    <section>
      <h2 className="mb-2 font-semibold text-slate-800">{titulo}</h2>
      <div className="flex flex-wrap items-end gap-3">{children}</div>
    </section>
  );
}

/** Desenha o campo conforme o tipo que o SER declarou para ele. */
function CampoDinamico({
  c,
  valor,
  onChange,
}: {
  c: CampoDinamicoSer;
  valor: string;
  onChange: (v: string) => void;
}) {
  const rotulo = `${c.rotulo}${c.obrigatorio ? ' *' : ''}`;
  const id = `din-${c.numero}`;

  if (c.tipo === 'textarea') {
    return (
      <Campo label={rotulo} htmlFor={id} className="min-w-96 flex-1">
        <textarea
          id={id}
          rows={3}
          value={valor}
          onChange={(e) => onChange(e.target.value)}
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
        />
      </Campo>
    );
  }

  if (c.tipo === 'select' && c.opcoes) {
    return (
      <Campo label={rotulo} htmlFor={id} className="min-w-72">
        <Select id={id} value={valor} onChange={(e) => onChange(e.target.value)}>
          <option value="">Selecione…</option>
          {c.opcoes.map((o: OpcaoSer) => (
            <option key={o.valor} value={o.valor}>{o.rotulo}</option>
          ))}
        </Select>
      </Campo>
    );
  }

  if (c.tipo === 'radio' && c.opcoes) {
    return (
      <Campo label={rotulo} htmlFor={id} className="min-w-72">
        <div id={id} className="flex flex-wrap gap-3 pt-1">
          {c.opcoes.map((o: OpcaoSer) => (
            <label key={o.valor} className="flex items-center gap-1.5 text-sm">
              <input
                type="radio"
                name={c.campo}
                value={o.valor}
                checked={valor === o.valor}
                onChange={() => onChange(o.valor)}
              />
              {o.rotulo}
            </label>
          ))}
        </div>
      </Campo>
    );
  }

  return (
    <Campo label={rotulo} htmlFor={id} className="w-56">
      <Input id={id} value={valor} onChange={(e) => onChange(e.target.value)} />
    </Campo>
  );
}
