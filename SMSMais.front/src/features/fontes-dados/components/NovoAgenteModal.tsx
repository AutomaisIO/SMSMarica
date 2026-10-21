import { useEffect, useState } from 'react';
import { Loader2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { useProvisionarAgente } from '@/features/fontes-dados/api/fontesDadosApi';
import { TokenAgenteGerado } from '@/features/fontes-dados/components/TokenAgenteGerado';
import type { TokenAgente } from '@/features/fontes-dados/types';

/**
 * Base SQL Server de uma unidade (ADR-0023): o agente instalado no servidor dela conecta na
 * plataforma, e as credenciais do banco ficam no .env dele. Aqui só se reserva o slug e se gera o
 * token — o mesmo que o instalador do agente faz sozinho no primeiro uso.
 */
export function NovoAgenteModal({ aberto, aoFechar }: { aberto: boolean; aoFechar: () => void }) {
  const provisionar = useProvisionarAgente();
  const [slug, setSlug] = useState('');
  const [nome, setNome] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [token, setToken] = useState<TokenAgente | null>(null);

  useEffect(() => {
    if (aberto) {
      setSlug('');
      setNome('');
      setErro(null);
      setToken(null);
    }
  }, [aberto]);

  function aoGerar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    if (!slug.trim()) {
      setErro('Informe o slug do agente.');
      return;
    }
    provisionar.mutate(
      { slug: slug.trim(), nome: nome.trim() || undefined },
      { onSuccess: setToken, onError: (err) => setErro(extrairMensagemDeErro(err)) },
    );
  }

  return (
    <Modal
      aberto={aberto}
      aoFechar={aoFechar}
      titulo="Novo agente"
      descricao="Banco SQL Server lido por um agente instalado no servidor da unidade."
      largura="lg"
    >
      {token ? (
        <div className="space-y-4">
          <TokenAgenteGerado token={token} />
          <div className="flex justify-end">
            <Button type="button" onClick={aoFechar}>
              Pronto
            </Button>
          </div>
        </div>
      ) : (
        <form onSubmit={aoGerar} className="space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Campo
              label="Slug"
              htmlFor="ag-slug"
              required
              dica="Identifica o agente e marca a origem dos dados importados (ex.: upa24h-marica-sqlserver). Não mude depois."
            >
              <Input id="ag-slug" value={slug} onChange={(e) => setSlug(e.target.value)} placeholder="unidade-sqlserver" />
            </Campo>
            <Campo label="Nome" htmlFor="ag-nome">
              <Input id="ag-nome" value={nome} onChange={(e) => setNome(e.target.value)} placeholder="UPA (SQL Server)" />
            </Campo>
          </div>
          <p className="text-xs text-gray-500">
            Se o slug já existe, um token novo é gerado — e o agente que usava o anterior desconecta até
            receber o novo.
          </p>
          {erro ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
          ) : null}
          <div className="flex justify-end gap-2">
            <Button type="button" variante="ghost" onClick={aoFechar}>
              Cancelar
            </Button>
            <Button type="submit" disabled={provisionar.isPending}>
              {provisionar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
              Gerar token
            </Button>
          </div>
        </form>
      )}
    </Modal>
  );
}
