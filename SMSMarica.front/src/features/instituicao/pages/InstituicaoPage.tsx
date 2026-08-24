import { useEffect, useRef, useState } from 'react';
import { Landmark, Upload } from 'lucide-react';
import { useInstituicao, useSalvarInstituicao } from '../queries';
import type { Instituicao, SalvarInstituicaoPayload } from '../api';
import { enviarMidia, urlMidiaAbsoluta } from '@/shared/api/midiaApi';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { notificar } from '@/shared/ui/Notificacoes';
import { usePermissao } from '@/shared/auth/authStore';

/**
 * Identidade da instituição desta instância (ADR-0043).
 *
 * O que se edita aqui não é configuração operacional: sai na tela de login, no PDF de laudo e
 * na página pública de verificação de documento — inclusive para quem não está autenticado.
 * É por isso que a tela é explícita sobre o alcance de cada campo.
 */
export function InstituicaoPage() {
  const { data, isLoading, error } = useInstituicao();
  const salvar = useSalvarInstituicao();
  const podeEditar = usePermissao('Instituicao', 'Edicao');

  const [form, setForm] = useState<SalvarInstituicaoPayload | null>(null);

  useEffect(() => {
    if (data) setForm(paraPayload(data));
  }, [data]);

  if (isLoading) return <p className="p-6 text-sm text-gray-500">Carregando…</p>;
  if (error) return <p className="p-6 text-sm text-error-600">{extrairMensagemDeErro(error)}</p>;
  if (!form) return null;

  const set = <K extends keyof SalvarInstituicaoPayload>(campo: K, valor: SalvarInstituicaoPayload[K]) =>
    setForm((f) => (f ? { ...f, [campo]: valor } : f));

  async function submeter(e: React.FormEvent) {
    e.preventDefault();
    if (!form) return;
    try {
      await salvar.mutateAsync(form);
      notificar('Identidade da instituição salva.', 'sucesso');
    } catch (err) {
      notificar(extrairMensagemDeErro(err), 'erro');
    }
  }

  return (
    <form onSubmit={submeter} className="mx-auto max-w-4xl space-y-6 p-6">
      <header className="flex items-start gap-3">
        <div className="icon-box icon-box-default">
          <Landmark className="h-5 w-5" />
        </div>
        <div>
          <h1>Instituição</h1>
          <p className="mt-1 text-sm text-gray-600">
            Identidade desta instalação. Estes dados aparecem no login, nos documentos em PDF e
            nas páginas públicas — inclusive para quem não fez login.
          </p>
        </div>
      </header>

      <Secao titulo="Identificação">
        <Campo rotulo="Prefeitura / ente" obrigatorio>
          <input className="input" value={form.nome} onChange={(e) => set('nome', e.target.value)} required />
        </Campo>
        <Campo
          rotulo="Secretaria"
          obrigatorio
          ajuda="Encabeça laudos, declarações e a página de verificação de documento."
        >
          <input
            className="input"
            value={form.nomeSecretaria}
            onChange={(e) => set('nomeSecretaria', e.target.value)}
            required
          />
        </Campo>
        <Campo rotulo="Nome curto" obrigatorio ajuda="Título da aba e cabeçalho das páginas públicas.">
          <input
            className="input"
            value={form.nomeCurto}
            onChange={(e) => set('nomeCurto', e.target.value)}
            required
          />
        </Campo>
        <div className="grid gap-4 sm:grid-cols-3">
          <Campo rotulo="Sigla">
            <input className="input" value={form.sigla ?? ''} onChange={(e) => set('sigla', vazioNulo(e.target.value))} />
          </Campo>
          <Campo rotulo="UF" obrigatorio>
            <input
              className="input uppercase"
              maxLength={2}
              value={form.uf}
              onChange={(e) => set('uf', e.target.value.toUpperCase())}
              required
            />
          </Campo>
          <Campo rotulo="DDD" ajuda="Completa telefones digitados sem DDD.">
            <input
              className="input"
              inputMode="numeric"
              value={form.dddPadrao ?? ''}
              onChange={(e) => set('dddPadrao', e.target.value ? Number(e.target.value) : null)}
            />
          </Campo>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <Campo rotulo="CNPJ">
            <input className="input" value={form.cnpj ?? ''} onChange={(e) => set('cnpj', vazioNulo(e.target.value))} />
          </Campo>
          <Campo rotulo="Código IBGE do município" ajuda="7 dígitos. Usado por integrações federais.">
            <input
              className="input"
              value={form.codigoIbge ?? ''}
              onChange={(e) => set('codigoIbge', vazioNulo(e.target.value))}
            />
          </Campo>
        </div>
      </Secao>

      <Secao titulo="Contato e LGPD">
        <div className="grid gap-4 sm:grid-cols-2">
          <Campo rotulo="Telefone">
            <input
              className="input"
              value={form.telefone ?? ''}
              onChange={(e) => set('telefone', vazioNulo(e.target.value))}
            />
          </Campo>
          <Campo rotulo="WhatsApp divulgado ao cidadão" ajuda="É o número que o app mostra, não o que envia.">
            <input
              className="input"
              value={form.whatsAppNumeroPublico ?? ''}
              onChange={(e) => set('whatsAppNumeroPublico', vazioNulo(e.target.value))}
            />
          </Campo>
          <Campo rotulo="E-mail de contato">
            <input
              className="input"
              type="email"
              value={form.emailContato ?? ''}
              onChange={(e) => set('emailContato', vazioNulo(e.target.value))}
            />
          </Campo>
          <Campo
            rotulo="E-mail do encarregado (DPO)"
            ajuda="Citado no termo de consentimento e nas páginas legais. Art. 41 da LGPD."
          >
            <input
              className="input"
              type="email"
              value={form.emailDpo ?? ''}
              onChange={(e) => set('emailDpo', vazioNulo(e.target.value))}
            />
          </Campo>
        </div>
      </Secao>

      <Secao titulo="Marca">
        <div className="grid gap-4 sm:grid-cols-2">
          <ImagemCampo
            rotulo="Logotipo"
            ajuda="PNG com fundo transparente funciona melhor sobre o menu."
            midiaId={form.logoMidiaId}
            categoria="instituicao-logo"
            onTrocar={(id) => set('logoMidiaId', id)}
          />
          <ImagemCampo
            rotulo="Favicon"
            ajuda="Ícone da aba do navegador."
            midiaId={form.faviconMidiaId}
            categoria="instituicao-favicon"
            onTrocar={(id) => set('faviconMidiaId', id)}
          />
        </div>

        <p className="text-xs text-gray-500">
          Deixe as cores em branco para manter a paleta padrão do sistema. Informando só a cor
          principal, os demais tons são derivados dela.
        </p>
        <div className="grid gap-4 sm:grid-cols-2">
          <CorCampo rotulo="Cor principal" valor={form.corPrimaria} onChange={(v) => set('corPrimaria', v)} />
          <CorCampo rotulo="Cor de apoio" valor={form.corSecundaria} onChange={(v) => set('corSecundaria', v)} />
        </div>
      </Secao>

      <Secao titulo="Endereços desta instância" descricao="Usados em links enviados ao cidadão.">
        <div className="grid gap-4 sm:grid-cols-3">
          <Campo rotulo="Painel">
            <input
              className="input"
              placeholder="https://…"
              value={form.urlPainel ?? ''}
              onChange={(e) => set('urlPainel', vazioNulo(e.target.value))}
            />
          </Campo>
          <Campo rotulo="App do cidadão">
            <input
              className="input"
              placeholder="https://…"
              value={form.urlApp ?? ''}
              onChange={(e) => set('urlApp', vazioNulo(e.target.value))}
            />
          </Campo>
          <Campo rotulo="PWA de arquivos">
            <input
              className="input"
              placeholder="https://…"
              value={form.urlArquivos ?? ''}
              onChange={(e) => set('urlArquivos', vazioNulo(e.target.value))}
            />
          </Campo>
        </div>
      </Secao>

      <div className="flex items-center justify-end gap-3">
        {!podeEditar && (
          <span className="text-xs text-gray-500">Você não tem permissão para alterar.</span>
        )}
        <button type="submit" className="btn btn-primary" disabled={!podeEditar || salvar.isPending}>
          {salvar.isPending ? 'Salvando…' : 'Salvar'}
        </button>
      </div>
    </form>
  );
}

// ---- peças da tela ----

function Secao({ titulo, descricao, children }: { titulo: string; descricao?: string; children: React.ReactNode }) {
  return (
    <section className="card space-y-4 p-5">
      <div>
        <h3>{titulo}</h3>
        {descricao && <p className="mt-1 text-sm text-gray-600">{descricao}</p>}
      </div>
      {children}
    </section>
  );
}

function Campo({
  rotulo,
  ajuda,
  obrigatorio,
  children,
}: {
  rotulo: string;
  ajuda?: string;
  obrigatorio?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div>
      <label className="label">
        {rotulo}
        {obrigatorio && <span className="ml-1 text-error-500">*</span>}
      </label>
      {children}
      {ajuda && <p className="mt-1 text-xs text-gray-500">{ajuda}</p>}
    </div>
  );
}

/**
 * Seletor de cor. O `type="color"` do browser só devolve `#rrggbb`, que é exatamente o que o
 * backend aceita — usar um campo de texto livre aqui só criaria erro de validação para o
 * operador descobrir depois.
 */
function CorCampo({
  rotulo,
  valor,
  onChange,
}: {
  rotulo: string;
  valor: string | null;
  onChange: (v: string | null) => void;
}) {
  return (
    <div>
      <label className="label">{rotulo}</label>
      <div className="flex items-center gap-3">
        <input
          type="color"
          className="h-9 w-14 cursor-pointer rounded border border-gray-300 bg-white p-1"
          value={valor ?? '#C8102E'}
          onChange={(e) => onChange(e.target.value.toUpperCase())}
        />
        <code className="text-sm text-gray-600">{valor ?? 'padrão do sistema'}</code>
        {valor && (
          <button type="button" className="btn btn-ghost btn-sm" onClick={() => onChange(null)}>
            Limpar
          </button>
        )}
      </div>
    </div>
  );
}

function ImagemCampo({
  rotulo,
  ajuda,
  midiaId,
  categoria,
  onTrocar,
}: {
  rotulo: string;
  ajuda?: string;
  midiaId: string | null;
  categoria: string;
  onTrocar: (id: string | null) => void;
}) {
  const input = useRef<HTMLInputElement>(null);
  const [enviando, setEnviando] = useState(false);

  async function selecionar(e: React.ChangeEvent<HTMLInputElement>) {
    const arquivo = e.target.files?.[0];
    if (!arquivo) return;
    setEnviando(true);
    try {
      const midia = await enviarMidia(arquivo, categoria);
      onTrocar(midia.id);
    } catch (err) {
      notificar(extrairMensagemDeErro(err), 'erro');
    } finally {
      setEnviando(false);
      if (input.current) input.current.value = '';
    }
  }

  return (
    <div>
      <label className="label">{rotulo}</label>
      <div className="flex items-center gap-3">
        <div className="flex h-16 w-28 items-center justify-center overflow-hidden rounded border border-gray-200 bg-gray-50">
          {midiaId ? (
            <img src={urlMidiaAbsoluta(midiaId)} alt={rotulo} className="max-h-full max-w-full object-contain" />
          ) : (
            <span className="text-xs text-gray-400">sem imagem</span>
          )}
        </div>
        <div className="flex flex-col gap-1">
          <button
            type="button"
            className="btn btn-outline btn-sm"
            onClick={() => input.current?.click()}
            disabled={enviando}
          >
            <Upload className="h-4 w-4" />
            {enviando ? 'Enviando…' : 'Enviar'}
          </button>
          {midiaId && (
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => onTrocar(null)}>
              Remover
            </button>
          )}
        </div>
        <input ref={input} type="file" accept="image/*" className="hidden" onChange={selecionar} />
      </div>
      {ajuda && <p className="mt-1 text-xs text-gray-500">{ajuda}</p>}
    </div>
  );
}

// ---- helpers ----

const vazioNulo = (v: string): string | null => (v.trim() === '' ? null : v);

function paraPayload(i: Instituicao): SalvarInstituicaoPayload {
  return {
    nome: i.nome,
    nomeSecretaria: i.nomeSecretaria,
    nomeCurto: i.nomeCurto,
    sigla: i.sigla,
    cnpj: i.cnpj,
    codigoIbge: i.codigoIbge,
    uf: i.uf,
    dddPadrao: i.dddPadrao,
    endereco: null,
    telefone: i.telefone,
    emailContato: i.emailContato,
    emailDpo: i.emailDpo,
    whatsAppNumeroPublico: i.whatsAppNumeroPublico,
    logoMidiaId: i.logoMidiaId,
    faviconMidiaId: i.faviconMidiaId,
    corPrimaria: i.corPrimaria,
    corSecundaria: i.corSecundaria,
    corGradienteInicio: i.corGradienteInicio,
    corGradienteFim: i.corGradienteFim,
    urlPainel: i.urlPainel,
    urlApp: i.urlApp,
    urlArquivos: i.urlArquivos,
    assinaturaProdutoHtml: i.assinaturaProdutoHtml,
  };
}
