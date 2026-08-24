import { useEffect, useState } from 'react';
import { Loader2, PlugZap } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import {
  useCriarFonteConfig,
  useAtualizarFonteConfig,
  useTestarConexaoFonte,
} from '@/features/ia/api/queries';
import type {
  Ambiente,
  FonteConfig,
  ResultadoTesteConexao,
  SalvarFonteConfigPayload,
} from '@/features/ia/types';

type Props = {
  aberto: boolean;
  aoFechar: () => void;
  /** undefined => criação; objeto => edição. */
  fonte?: FonteConfig;
};

type FormState = {
  nome: string;
  slug: string;
  tipo: string;
  dialeto: string;
  ambiente: Ambiente;
  host: string;
  porta: string;
  servico: string;
  usuario: string;
  baseUrl: string;
  senha: string;
  ativo: boolean;
};

function estadoInicial(fonte?: FonteConfig): FormState {
  return {
    nome: fonte?.nome ?? '',
    slug: fonte?.slug ?? '',
    tipo: fonte?.tipo ?? 'Salux',
    dialeto: fonte?.dialeto ?? 'oracle',
    ambiente: fonte?.ambiente ?? 'TREINAMENTO',
    host: fonte?.host ?? '',
    porta: fonte?.porta != null ? String(fonte.porta) : '',
    servico: fonte?.servico ?? '',
    usuario: fonte?.usuario ?? '',
    baseUrl: fonte?.baseUrl ?? '',
    senha: '',
    ativo: fonte?.ativo ?? true,
  };
}

export function FonteConfigModal({ aberto, aoFechar, fonte }: Props) {
  const ehEdicao = Boolean(fonte);
  const [form, setForm] = useState<FormState>(() => estadoInicial(fonte));
  const [erro, setErro] = useState<string | null>(null);
  const [resultadoTeste, setResultadoTeste] = useState<ResultadoTesteConexao | null>(null);

  const criar = useCriarFonteConfig();
  const atualizar = useAtualizarFonteConfig();
  const testar = useTestarConexaoFonte();

  useEffect(() => {
    if (aberto) {
      setForm(estadoInicial(fonte));
      setErro(null);
      setResultadoTeste(null);
    }
  }, [aberto, fonte]);

  function set<K extends keyof FormState>(chave: K, valor: FormState[K]) {
    setForm((f) => ({ ...f, [chave]: valor }));
  }

  function montarPayload(): SalvarFonteConfigPayload {
    return {
      nome: form.nome.trim(),
      slug: form.slug.trim() || undefined,
      tipo: form.tipo.trim(),
      dialeto: form.dialeto.trim(),
      ambiente: form.ambiente,
      host: form.host.trim() || undefined,
      porta: form.porta.trim() ? Number(form.porta) : undefined,
      servico: form.servico.trim() || undefined,
      usuario: form.usuario.trim() || undefined,
      baseUrl: form.baseUrl.trim() || undefined,
      senha: form.senha ? form.senha : undefined,
      ativo: form.ativo,
    };
  }

  function aoSalvar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    const payload = montarPayload();
    if (ehEdicao && fonte) {
      atualizar.mutate(
        { id: fonte.id, payload },
        { onSuccess: aoFechar, onError: (err) => setErro(extrairMensagemDeErro(err)) },
      );
    } else {
      criar.mutate(payload, {
        onSuccess: aoFechar,
        onError: (err) => setErro(extrairMensagemDeErro(err)),
      });
    }
  }

  function aoTestar() {
    if (!fonte) return; // só testa fontes já salvas
    setResultadoTeste(null);
    testar.mutate(fonte.id, {
      onSuccess: (r) => setResultadoTeste(r),
      onError: (err) => setResultadoTeste({ sucesso: false, mensagem: extrairMensagemDeErro(err) }),
    });
  }

  const salvando = criar.isPending || atualizar.isPending;

  return (
    <Modal
      aberto={aberto}
      aoFechar={aoFechar}
      titulo={ehEdicao ? 'Editar base' : 'Nova base'}
      descricao="Conexão a uma base de dados consultável pela IA."
      largura="lg"
    >
      <form onSubmit={aoSalvar} className="space-y-4">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Campo label="Nome" htmlFor="fc-nome" required>
            <Input
              id="fc-nome"
              value={form.nome}
              onChange={(e) => set('nome', e.target.value)}
              placeholder="Ex.: Salux Produção"
            />
          </Campo>

          <Campo
            label="Slug (identificador da base)"
            htmlFor="fc-slug"
            dica="Curto e estável (ex.: salux-hcml). Identifica a origem na importação e evita colisão entre hospitais. Não mude depois de importar."
          >
            <Input
              id="fc-slug"
              value={form.slug}
              onChange={(e) => set('slug', e.target.value)}
              placeholder="salux-hcml"
            />
          </Campo>

          <Campo label="Tipo" htmlFor="fc-tipo" required dica="Família da base (não o motor de banco).">
            <Select
              id="fc-tipo"
              value={form.tipo}
              onChange={(e) => {
                const t = e.target.value;
                // Sugere o dialeto conforme o tipo (ajustável).
                const dialeto = t === 'Postgres' ? 'postgres' : t === 'Fhir' ? '' : 'oracle';
                setForm((f) => ({ ...f, tipo: t, dialeto }));
              }}
            >
              <option value="Salux">Salux</option>
              <option value="Mv">MV</option>
              <option value="Eco">ECO</option>
              <option value="Fhir">FHIR (API REST)</option>
              <option value="Postgres">PostgreSQL</option>
            </Select>
          </Campo>

          <Campo label="Dialeto" htmlFor="fc-dialeto" dica="Dialeto SQL usado na geração das consultas.">
            <Select id="fc-dialeto" value={form.dialeto} onChange={(e) => set('dialeto', e.target.value)}>
              <option value="oracle">Oracle</option>
              <option value="postgres">PostgreSQL</option>
            </Select>
          </Campo>

          <Campo label="Ambiente" htmlFor="fc-ambiente" required>
            <Select
              id="fc-ambiente"
              value={form.ambiente}
              onChange={(e) => set('ambiente', e.target.value as Ambiente)}
            >
              <option value="TREINAMENTO">TREINAMENTO</option>
              <option value="PRODUCAO">PRODUCAO</option>
            </Select>
          </Campo>

          <Campo label="Host / IP" htmlFor="fc-host">
            <Input
              id="fc-host"
              value={form.host}
              onChange={(e) => set('host', e.target.value)}
              placeholder="10.50.0.18"
            />
          </Campo>

          <Campo label="Porta" htmlFor="fc-porta">
            <Input
              id="fc-porta"
              type="number"
              value={form.porta}
              onChange={(e) => set('porta', e.target.value)}
              placeholder="1521"
            />
          </Campo>

          <Campo label="Serviço / SID" htmlFor="fc-servico">
            <Input
              id="fc-servico"
              value={form.servico}
              onChange={(e) => set('servico', e.target.value)}
              placeholder="ORCL"
            />
          </Campo>

          <Campo label="Usuário" htmlFor="fc-usuario">
            <Input
              id="fc-usuario"
              value={form.usuario}
              onChange={(e) => set('usuario', e.target.value)}
              autoComplete="off"
            />
          </Campo>

          <Campo
            label="Senha"
            htmlFor="fc-senha"
            dica={fonte?.senhaDefinida ? 'Já definida — preencha apenas para alterar.' : undefined}
          >
            <Input
              id="fc-senha"
              type="password"
              value={form.senha}
              onChange={(e) => set('senha', e.target.value)}
              placeholder={fonte?.senhaDefinida ? '••••••••' : ''}
              autoComplete="new-password"
            />
          </Campo>

          {form.tipo === 'Fhir' ? (
            <Campo label="URL base" htmlFor="fc-baseurl" className="sm:col-span-2">
              <Input
                id="fc-baseurl"
                value={form.baseUrl}
                onChange={(e) => set('baseUrl', e.target.value)}
                placeholder="https://api.exemplo.com"
              />
            </Campo>
          ) : null}

          <label className="flex items-center gap-2 sm:col-span-2">
            <input
              type="checkbox"
              checked={form.ativo}
              onChange={(e) => set('ativo', e.target.checked)}
              className="h-4 w-4 rounded border-gray-300 text-primary-600"
            />
            <span className="text-sm text-gray-700">Base ativa (disponível para consulta)</span>
          </label>
        </div>

        {erro ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erro}
          </div>
        ) : null}

        {resultadoTeste ? (
          <div
            className={
              resultadoTeste.sucesso
                ? 'rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700'
                : 'rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700'
            }
          >
            {resultadoTeste.mensagem}
          </div>
        ) : null}

        <div className="flex items-center justify-between gap-3 border-t border-gray-100 pt-4">
          <Button
            type="button"
            variante="outline"
            onClick={aoTestar}
            disabled={!fonte || testar.isPending}
            title={fonte ? 'Testar conexão' : 'Salve a base antes de testar'}
          >
            {testar.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <PlugZap className="mr-2 h-4 w-4" />
            )}
            Testar conexão
          </Button>
          <div className="flex items-center gap-2">
            <Button type="button" variante="ghost" onClick={aoFechar}>
              Cancelar
            </Button>
            <Button type="submit" disabled={salvando || !form.nome.trim()}>
              {salvando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              Salvar
            </Button>
          </div>
        </div>
      </form>
    </Modal>
  );
}
