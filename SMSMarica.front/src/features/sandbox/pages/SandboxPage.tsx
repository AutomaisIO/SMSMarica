import { useState } from 'react';
import { Check, Copy, FlaskConical, Search, Send } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import {
  sandboxApi,
  type ResultadoEnvio,
  type SandboxPaciente,
  type SandboxSolicitacao,
} from '@/features/sandbox/sandboxApi';

// Corpo do template confirma_exame (mesmo texto aprovado), para colar/editar no teste.
const MODELO_CONFIRMA_EXAME =
  '📆 Olá *{nome}*, você tem um exame de {exame} agendado para {data}, {unidade}📍, às {hora}. ' +
  '*É muito importante sua confirmação.* 😊';

export function SandboxPage() {
  const [termo, setTermo] = useState('');
  const [pacientes, setPacientes] = useState<SandboxPaciente[]>([]);
  const [buscando, setBuscando] = useState(false);
  const [sel, setSel] = useState<SandboxPaciente | null>(null);
  const [solic, setSolic] = useState<SandboxSolicitacao[]>([]);

  const [telefone, setTelefone] = useState('');
  const [texto, setTexto] = useState(MODELO_CONFIRMA_EXAME);
  const [comLink, setComLink] = useState(true);
  const [destino, setDestino] = useState('/agendados/exames');
  const [enviando, setEnviando] = useState(false);
  const [resultado, setResultado] = useState<ResultadoEnvio | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [linkGerado, setLinkGerado] = useState<string | null>(null);
  const [copiado, setCopiado] = useState(false);

  async function buscar() {
    if (termo.trim().length < 3) return;
    setBuscando(true);
    setErro(null);
    try {
      setPacientes(await sandboxApi.buscarPacientes(termo.trim()));
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setBuscando(false);
    }
  }

  async function selecionar(p: SandboxPaciente) {
    setSel(p);
    setResultado(null);
    setLinkGerado(null);
    try {
      setSolic(await sandboxApi.solicitacoes(p.id));
    } catch {
      setSolic([]);
    }
  }

  async function gerarLink() {
    if (!sel) return;
    setErro(null);
    try {
      const r = await sandboxApi.gerarLink(sel.id, destino || undefined);
      setLinkGerado(r.url);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function enviar() {
    if (!telefone.trim()) {
      setErro('Informe o telefone de destino.');
      return;
    }
    setEnviando(true);
    setErro(null);
    setResultado(null);
    try {
      const r = await sandboxApi.enviar({
        telefone: telefone.trim(),
        texto,
        pacienteId: comLink && sel ? sel.id : undefined,
        destino: destino || undefined,
      });
      setResultado(r);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  async function definirConfirmacao(s: SandboxSolicitacao, estado: string) {
    setErro(null);
    try {
      await sandboxApi.definirConfirmacao(s.id, estado);
      if (sel) setSolic(await sandboxApi.solicitacoes(sel.id));
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  function copiar(v: string) {
    navigator.clipboard?.writeText(v);
    setCopiado(true);
    setTimeout(() => setCopiado(false), 1500);
  }

  return (
    <div className="space-y-6">
      <header className="flex items-start gap-3">
        <FlaskConical className="mt-1 h-6 w-6 text-red-600" />
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Sandbox de testes (QA)</h1>
          <p className="mt-1 text-sm text-gray-600">
            Opera sobre um <strong>paciente real</strong> que você escolher, para testar o magic link, a
            visualização no PWA e o fluxo de confirmação. As mensagens são <strong>texto livre</strong> —
            só chegam se a janela de 24h estiver aberta (mande uma mensagem ao número de produção primeiro).
          </p>
        </div>
      </header>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}

      {/* 1. Escolher paciente */}
      <section className="rounded-lg border border-gray-200 p-4">
        <p className="mb-2 text-sm font-semibold text-gray-800">1. Escolha o paciente real</p>
        <div className="flex gap-2">
          <div className="relative flex-1">
            <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
            <Input
              value={termo}
              onChange={(e) => setTermo(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && buscar()}
              placeholder="Nome ou CPF (mín. 3 caracteres)"
              className="pl-9"
            />
          </div>
          <Button onClick={buscar} disabled={buscando}>Buscar</Button>
        </div>
        {pacientes.length > 0 ? (
          <ul className="mt-3 divide-y divide-gray-100 rounded-md border border-gray-100">
            {pacientes.map((p) => (
              <li key={p.id}>
                <button
                  type="button"
                  onClick={() => selecionar(p)}
                  className={`flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-gray-50 ${
                    sel?.id === p.id ? 'bg-red-50' : ''
                  }`}
                >
                  <span className="font-medium text-gray-900">{p.nome}</span>
                  <span className="font-mono text-xs text-gray-500">{p.cpf ?? '—'}</span>
                </button>
              </li>
            ))}
          </ul>
        ) : null}
        {sel ? (
          <p className="mt-2 text-sm text-gray-600">
            Selecionado: <strong>{sel.nome}</strong>
          </p>
        ) : null}
      </section>

      {/* 2. Enviar mensagem de teste */}
      <section className="rounded-lg border border-gray-200 p-4">
        <p className="mb-2 text-sm font-semibold text-gray-800">2. Enviar mensagem de teste (texto livre)</p>
        <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
          <div>
            <label className="text-xs font-medium text-gray-500">Telefone de destino</label>
            <Input
              value={telefone}
              onChange={(e) => setTelefone(e.target.value)}
              placeholder="Ex.: 21999990000 (o seu, com sessão aberta)"
            />
          </div>
          <div>
            <label className="text-xs font-medium text-gray-500">Destino no app (após o login)</label>
            <Input value={destino} onChange={(e) => setDestino(e.target.value)} placeholder="/agendados/exames" />
          </div>
        </div>
        <label className="mt-3 block text-xs font-medium text-gray-500">Mensagem (edite à vontade)</label>
        <textarea
          className="mt-1 w-full rounded-md border border-gray-200 p-2 text-sm"
          rows={4}
          value={texto}
          onChange={(e) => setTexto(e.target.value)}
        />
        <label className="mt-2 flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" checked={comLink} onChange={(e) => setComLink(e.target.checked)} disabled={!sel} />
          Anexar magic link do paciente selecionado ({sel ? sel.nome : 'escolha um paciente'})
        </label>
        <div className="mt-3 flex items-center gap-2">
          <Button onClick={enviar} disabled={enviando}>
            <Send className="mr-1.5 h-4 w-4" />
            Enviar
          </Button>
          <Button variante="outline" onClick={gerarLink} disabled={!sel}>
            Só gerar o link
          </Button>
        </div>

        {resultado ? (
          <div
            className={`mt-3 rounded-md px-3 py-2 text-sm ${
              resultado.ok ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'
            }`}
          >
            {resultado.ok
              ? 'Enviado! Se a janela de 24h estiver aberta, chega no WhatsApp.'
              : `Falha: ${resultado.erro ?? 'erro desconhecido'}`}
            {resultado.link ? <LinkCopiavel url={resultado.link} onCopiar={copiar} copiado={copiado} /> : null}
          </div>
        ) : null}
        {linkGerado ? (
          <div className="mt-3 rounded-md bg-gray-50 px-3 py-2 text-sm">
            <LinkCopiavel url={linkGerado} onCopiar={copiar} copiado={copiado} />
          </div>
        ) : null}
      </section>

      {/* 3. Forçar estado de confirmação */}
      {sel ? (
        <section className="rounded-lg border border-gray-200 p-4">
          <p className="mb-2 text-sm font-semibold text-gray-800">
            3. Forçar estado de confirmação (reversível) — solicitações de {sel.nome}
          </p>
          {solic.length === 0 ? (
            <p className="text-sm text-gray-500">Este paciente não tem solicitações de exame.</p>
          ) : (
            <ul className="divide-y divide-gray-100 rounded-md border border-gray-100">
              {solic.map((s) => (
                <li key={s.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2 text-sm">
                  <div className="min-w-0">
                    <span className="font-medium text-gray-900">{s.tipoExame ?? 'Exame'}</span>{' '}
                    <span className="font-mono text-xs text-gray-500">{s.accessionNumber}</span>
                    <span className="ml-2 rounded bg-gray-100 px-1.5 py-0.5 text-xs text-gray-600">
                      conf.: {s.statusConfirmacao}
                    </span>
                  </div>
                  <div className="flex gap-1.5">
                    <Button tamanho="sm" variante="outline" onClick={() => definirConfirmacao(s, 'pendente')}>
                      Pendente
                    </Button>
                    <Button tamanho="sm" variante="outline" onClick={() => definirConfirmacao(s, 'confirmada')}>
                      Confirmada
                    </Button>
                    <Button tamanho="sm" variante="outline" onClick={() => definirConfirmacao(s, 'cancelada')}>
                      Cancelada
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>
      ) : null}
    </div>
  );
}

function LinkCopiavel({
  url,
  onCopiar,
  copiado,
}: {
  url: string;
  onCopiar: (v: string) => void;
  copiado: boolean;
}) {
  return (
    <div className="mt-2 flex items-center gap-2">
      <code className="min-w-0 flex-1 truncate rounded bg-white px-2 py-1 text-xs text-gray-700">{url}</code>
      <button type="button" onClick={() => onCopiar(url)} className="shrink-0 text-gray-500 hover:text-gray-800">
        {copiado ? <Check className="h-4 w-4 text-green-600" /> : <Copy className="h-4 w-4" />}
      </button>
    </div>
  );
}
