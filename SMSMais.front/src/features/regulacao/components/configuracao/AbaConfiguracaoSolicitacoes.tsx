import { useEffect, useState } from 'react';
import { Loader2, Save } from 'lucide-react';

import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { notificar } from '@/shared/ui/Notificacoes';

import { useConfiguracaoRegulacao, useSalvarConfiguracaoRegulacao } from '../../api/queries';
import type { ConfiguracaoRegulacao } from '../../types';

const TIPOS_ANEXO = [
  { valor: 'image/jpeg', rotulo: 'JPEG' },
  { valor: 'image/png', rotulo: 'PNG' },
  { valor: 'image/webp', rotulo: 'WebP' },
  { valor: 'application/pdf', rotulo: 'PDF' },
];

/**
 * Configuração do fluxo de Solicitações (plano 09), aba da Configuração da Regulação.
 *
 * <p>Só entra aqui o que muda sem deploy. O botão salva com `rowVersion`: se alguém tiver salvo
 * antes, o servidor responde 409 e a tela manda recarregar em vez de sobrescrever em silêncio —
 * é uma linha única, editada por mais de uma pessoa.</p>
 */
export function AbaConfiguracaoSolicitacoes() {
  const consulta = useConfiguracaoRegulacao();
  const salvar = useSalvarConfiguracaoRegulacao();
  const [form, setForm] = useState<ConfiguracaoRegulacao | null>(null);

  useEffect(() => {
    if (consulta.data) setForm(consulta.data);
  }, [consulta.data]);

  if (consulta.isLoading || !form) {
    return <p className="text-sm text-slate-500">Carregando…</p>;
  }

  function alterar<K extends keyof ConfiguracaoRegulacao>(campo: K, valor: ConfiguracaoRegulacao[K]) {
    setForm((f) => (f ? { ...f, [campo]: valor } : f));
  }

  function alternarTipo(tipo: string) {
    setForm((f) => {
      if (!f) return f;
      const tem = f.anexoTiposPermitidos.includes(tipo);
      return {
        ...f,
        anexoTiposPermitidos: tem
          ? f.anexoTiposPermitidos.filter((t) => t !== tipo)
          : [...f.anexoTiposPermitidos, tipo],
      };
    });
  }

  async function aoSalvar() {
    if (!form) return;
    try {
      const { atualizadoEm: _ae, atualizadoPorNome: _apn, ...payload } = form;
      const atualizado = await salvar.mutateAsync(payload);
      setForm(atualizado);
      notificar('Configuração salva.');
    } catch {
      // O 409 já chega ao usuário pelo interceptor com a mensagem do servidor ("alguém salvou
      // antes; recarregue"). Recarregar aqui traz a versão nova e destrava o próximo salvamento.
      await consulta.refetch();
    }
  }

  return (
    <div className="max-w-3xl space-y-6">
      <Secao titulo="Fluxo" descricao="O que a unidade solicitante pode fazer sozinha.">
        <Alternador
          rotulo="Permitir Externo mesmo havendo oferta interna"
          ajuda="Desligado, a solicitação vai para o SISREG sempre que Maricá executar o procedimento."
          valor={form.permitirExternoComInterno}
          aoMudar={(v) => alterar('permitirExternoComInterno', v)}
        />
        <Alternador
          rotulo="Solicitante enxerga a fila de todas as unidades"
          ajuda="Desligado, cada unidade vê só as próprias solicitações."
          valor={form.pontaPodeVerTodasUnidades}
          aoMudar={(v) => alterar('pontaPodeVerTodasUnidades', v)}
        />
        <Alternador
          rotulo="Exigir CPF para sair do rascunho"
          ajuda="O SERNIT não grava sem CPF. Desligar aqui deixa a solicitação travar só no envio."
          valor={form.exigirCpf}
          aoMudar={(v) => alterar('exigirCpf', v)}
        />
        <Campo rotulo="Rótulo da fila" ajuda="Como a fila de triagem aparece nas telas.">
          <Input
            value={form.rotuloFila}
            maxLength={60}
            onChange={(e) => alterar('rotuloFila', e.target.value)}
            className="max-w-xs"
          />
        </Campo>
      </Secao>

      <Secao titulo="SISREG">
        <Campo
          rotulo="Prazo de edição após incluir (dias)"
          ajuda="Janela em que o agente ainda ajusta no SISREG. O valor real se confirma no spike da tela de marcação."
        >
          <Input
            type="number"
            min={0}
            max={60}
            value={form.sisregPrazoEdicaoDias}
            onChange={(e) => alterar('sisregPrazoEdicaoDias', Number(e.target.value))}
            className="max-w-[8rem]"
          />
        </Campo>
      </Secao>

      <Secao
        titulo="Busca de procedimento"
        descricao="Calibração da busca por semelhança. Mexer aqui muda o que aparece na tela de abrir solicitação."
      >
        <Campo
          rotulo="Corte de distância (0 a 1)"
          ajuda="Distância de cosseno máxima aceita. Menor = mais rigoroso, devolve menos resultados."
        >
          <Input
            type="number"
            step={0.005}
            min={0}
            max={1}
            value={form.buscaCorteDistancia}
            onChange={(e) => alterar('buscaCorteDistancia', Number(e.target.value))}
            className="max-w-[8rem]"
          />
        </Campo>
        <Campo
          rotulo="Semelhança mínima para sugerir pareamento (0 a 1)"
          ajuda="Acima disso, o sincronismo propõe que duas origens são o mesmo procedimento. A proposta sempre passa por confirmação humana."
        >
          <Input
            type="number"
            step={0.005}
            min={0}
            max={1}
            value={form.buscaScoreSugestaoPareamento}
            onChange={(e) => alterar('buscaScoreSugestaoPareamento', Number(e.target.value))}
            className="max-w-[8rem]"
          />
        </Campo>
      </Secao>

      <Secao titulo="Anexos">
        <Campo rotulo="Tamanho máximo por arquivo (MB)">
          <Input
            type="number"
            min={1}
            max={50}
            value={form.anexoLimiteMb}
            onChange={(e) => alterar('anexoLimiteMb', Number(e.target.value))}
            className="max-w-[8rem]"
          />
        </Campo>
        <Campo
          rotulo="Tipos aceitos"
          ajuda="Imagens entram porque o caminho real é foto de celular tirada no balcão, não só PDF digitalizado."
        >
          <div className="flex flex-wrap gap-3">
            {TIPOS_ANEXO.map((t) => (
              <label key={t.valor} className="flex items-center gap-1.5 text-sm text-slate-700">
                <input
                  type="checkbox"
                  checked={form.anexoTiposPermitidos.includes(t.valor)}
                  onChange={() => alternarTipo(t.valor)}
                  className="size-4 accent-red-600"
                />
                {t.rotulo}
              </label>
            ))}
          </div>
        </Campo>
      </Secao>

      <div className="flex items-center gap-3 border-t border-slate-200 pt-4">
        <Button onClick={aoSalvar} disabled={salvar.isPending}>
          {salvar.isPending ? (
            <Loader2 className="mr-2 size-4 animate-spin" />
          ) : (
            <Save className="mr-2 size-4" />
          )}
          Salvar
        </Button>
        {form.atualizadoEm ? (
          <p className="text-xs text-slate-500">
            Última alteração em {new Date(form.atualizadoEm).toLocaleString('pt-BR')}
            {form.atualizadoPorNome ? ` por ${form.atualizadoPorNome}` : ''}.
          </p>
        ) : null}
      </div>
    </div>
  );
}

function Secao({
  titulo,
  descricao,
  children,
}: {
  titulo: string;
  descricao?: string;
  children: React.ReactNode;
}) {
  return (
    <section className="space-y-3">
      <div>
        <h3 className="text-sm font-semibold text-slate-800">{titulo}</h3>
        {descricao ? <p className="text-xs text-slate-500">{descricao}</p> : null}
      </div>
      <div className="space-y-3 rounded-md border border-slate-200 bg-white p-4">{children}</div>
    </section>
  );
}

function Campo({
  rotulo,
  ajuda,
  children,
}: {
  rotulo: string;
  ajuda?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-1">
      <p className="text-sm font-medium text-slate-700">{rotulo}</p>
      {ajuda ? <p className="text-xs text-slate-500">{ajuda}</p> : null}
      {children}
    </div>
  );
}

function Alternador({
  rotulo,
  ajuda,
  valor,
  aoMudar,
}: {
  rotulo: string;
  ajuda?: string;
  valor: boolean;
  aoMudar: (v: boolean) => void;
}) {
  return (
    <label className="flex cursor-pointer items-start gap-2">
      <input
        type="checkbox"
        checked={valor}
        onChange={(e) => aoMudar(e.target.checked)}
        className="mt-0.5 size-4 accent-red-600"
      />
      <span>
        <span className="block text-sm font-medium text-slate-700">{rotulo}</span>
        {ajuda ? <span className="block text-xs text-slate-500">{ajuda}</span> : null}
      </span>
    </label>
  );
}
