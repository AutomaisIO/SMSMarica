import { useMemo, useState } from 'react';
import { X } from 'lucide-react';
import { useIniciarConversa, useTemplates } from '@/features/conversas/api/queries';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { ROTULO_ASSUNTO, type AssuntoConversa } from '@/features/conversas/types';

type Props = {
  onFechar: () => void;
  onCriada: (id: string) => void;
};

const ASSUNTOS: AssuntoConversa[] = ['Tfd', 'MarcacaoConsulta', 'Duvida', 'Atendente', 'Outro'];

export function NovaConversaDialog({ onFechar, onCriada }: Props) {
  const { data: templates } = useTemplates(true);
  const iniciar = useIniciarConversa();

  const [telefone, setTelefone] = useState('');
  const [nomeContato, setNomeContato] = useState('');
  const [assunto, setAssunto] = useState<AssuntoConversa | ''>('');
  const [templateNome, setTemplateNome] = useState('');
  const [idioma, setIdioma] = useState('pt_BR');
  const [params, setParams] = useState<string[]>([]);
  const [erro, setErro] = useState<string | null>(null);

  const temTemplates = (templates?.length ?? 0) > 0;
  const selecionado = useMemo(
    () => templates?.find((t) => t.nome === templateNome) ?? null,
    [templates, templateNome],
  );

  function aoTrocarTemplate(nome: string) {
    setTemplateNome(nome);
    const t = templates?.find((x) => x.nome === nome);
    setIdioma(t?.idioma ?? 'pt_BR');
    setParams(Array.from({ length: t?.parametros ?? 0 }, () => ''));
  }

  async function aoEnviar() {
    setErro(null);
    if (!telefone.trim() || !templateNome.trim()) {
      setErro('Informe o telefone e o modelo.');
      return;
    }
    try {
      const id = await iniciar.mutateAsync({
        telefone: telefone.trim(),
        nomeContato: nomeContato.trim() || null,
        assunto: assunto || null,
        template: templateNome.trim(),
        idioma: idioma.trim() || 'pt_BR',
        parametros: params.map((p) => p.trim()),
      });
      onCriada(id);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-md rounded-lg bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-gray-200 px-4 py-3">
          <h2 className="text-sm font-semibold text-gray-900">Nova conversa</h2>
          <button type="button" onClick={onFechar} className="rounded p-1 text-gray-400 hover:bg-gray-100" aria-label="Fechar">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="space-y-3 p-4">
          <div>
            <label className="mb-1 block text-xs font-medium text-gray-600">Telefone (com DDD)</label>
            <input
              value={telefone}
              onChange={(e) => setTelefone(e.target.value)}
              placeholder="21 99999-9999"
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
            />
          </div>

          <div>
            <label className="mb-1 block text-xs font-medium text-gray-600">Nome do contato (opcional)</label>
            <input
              value={nomeContato}
              onChange={(e) => setNomeContato(e.target.value)}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
            />
          </div>

          <div>
            <label className="mb-1 block text-xs font-medium text-gray-600">Assunto</label>
            <select
              value={assunto}
              onChange={(e) => setAssunto(e.target.value as AssuntoConversa | '')}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
            >
              <option value="">—</option>
              {ASSUNTOS.map((a) => (
                <option key={a} value={a}>{ROTULO_ASSUNTO[a]}</option>
              ))}
            </select>
          </div>

          <div>
            <label className="mb-1 block text-xs font-medium text-gray-600">Modelo (template aprovado)</label>
            {temTemplates ? (
              <select
                value={templateNome}
                onChange={(e) => aoTrocarTemplate(e.target.value)}
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
              >
                <option value="">Selecione…</option>
                {templates?.map((t) => (
                  <option key={`${t.nome}:${t.idioma}`} value={t.nome}>
                    {t.nome} ({t.idioma})
                  </option>
                ))}
              </select>
            ) : (
              <input
                value={templateNome}
                onChange={(e) => setTemplateNome(e.target.value)}
                placeholder="nome_do_modelo"
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
              />
            )}
            {selecionado?.corpo && (
              <p className="mt-1 rounded bg-gray-50 p-2 text-xs text-gray-500">{selecionado.corpo}</p>
            )}
          </div>

          {params.map((p, i) => (
            <div key={i}>
              <label className="mb-1 block text-xs font-medium text-gray-600">{`Variável {{${i + 1}}}`}</label>
              <input
                value={p}
                onChange={(e) => setParams((arr) => arr.map((v, j) => (j === i ? e.target.value : v)))}
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-primary-400"
              />
            </div>
          ))}

          {erro && <p className="text-xs text-red-600">{erro}</p>}
        </div>

        <div className="flex justify-end gap-2 border-t border-gray-200 px-4 py-3">
          <button type="button" onClick={onFechar} className="rounded-md px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-100">
            Cancelar
          </button>
          <button
            type="button"
            onClick={() => void aoEnviar()}
            disabled={iniciar.isPending}
            className="rounded-md bg-primary-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50"
          >
            {iniciar.isPending ? 'Enviando…' : 'Iniciar conversa'}
          </button>
        </div>
      </div>
    </div>
  );
}
