import { useEffect, useState } from 'react';
import { Loader2, Save, Settings2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { notificar } from '@/shared/ui/Notificacoes';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { useAtualizarConfiguracaoOuvidoria, useConfiguracaoOuvidoria } from '@/features/ouvidoria/api/queries';
import { Textarea } from '@/features/ouvidoria/components/Textarea';
import type { OuvidoriaConfiguracaoDto } from '@/features/ouvidoria/types';

type CampoNumero = Exclude<keyof OuvidoriaConfiguracaoDto, 'notificarPorWhatsApp' | 'textoRecibo'>;

const CAMPOS: { chave: CampoNumero; rotulo: string; dica: string; min: number; max: number }[] = [
  { chave: 'prazoCidadaoDias', rotulo: 'Prazo de resposta ao cidadão (dias)', dica: 'Lei 13.460 art. 16: 30 dias.', min: 1, max: 90 },
  { chave: 'prorrogacaoDias', rotulo: 'Prorrogação (dias)', dica: 'Uma vez por manifestação, com justificativa. Padrão 30.', min: 1, max: 90 },
  { chave: 'prazoAreaDias', rotulo: 'Prazo da área — prioridade Normal (dias)', dica: 'Quanto a unidade/área tem para responder. Padrão 20.', min: 1, max: 60 },
  { chave: 'prazoAreaAltaDias', rotulo: 'Prazo da área — prioridade Alta (dias)', dica: 'Padrão 10.', min: 1, max: 60 },
  { chave: 'prazoAreaUrgenteDiasUteis', rotulo: 'Prazo da área — Urgente (dias ÚTEIS)', dica: 'Conta seg–sex. Padrão 2.', min: 1, max: 30 },
  { chave: 'complementacaoDias', rotulo: 'Prazo para o cidadão complementar (dias)', dica: 'Sem resposta, arquiva automaticamente. Padrão 20.', min: 1, max: 60 },
  { chave: 'arquivamentoAutomaticoDias', rotulo: 'Conclusão automática após resposta (dias)', dica: 'Respondida sem recurso vira Concluída. Padrão 30.', min: 1, max: 90 },
];

/** Prazos e notificação da ouvidoria desta instância (singleton no backend). */
export function ConfiguracaoOuvidoriaPage() {
  const podeEditar = usePermissao('OuvidoriaGestao', 'Edicao');
  const { data, isLoading, isError, error } = useConfiguracaoOuvidoria();
  const salvar = useAtualizarConfiguracaoOuvidoria();
  const [form, setForm] = useState<OuvidoriaConfiguracaoDto | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (data) setForm(data);
  }, [data]);

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    if (!form) return;
    setErro(null);
    for (const c of CAMPOS) {
      const v = form[c.chave];
      if (!Number.isInteger(v) || v < c.min || v > c.max) {
        setErro(`"${c.rotulo}" deve ser um inteiro entre ${c.min} e ${c.max}.`);
        return;
      }
    }
    try {
      await salvar.mutateAsync({ ...form, textoRecibo: form.textoRecibo?.trim() || null });
      notificar('Configuração salva.', 'sucesso');
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    }
  }

  if (isLoading || !form) {
    return isError ? (
      <p role="alert" className="p-6 text-sm text-red-600">
        {extrairMensagemDeErro(error)}
      </p>
    ) : (
      <p className="p-6 text-sm text-slate-500">Carregando…</p>
    );
  }

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div>
        <div className="flex items-center gap-1.5">
          <h1 className="flex items-center gap-2 text-xl font-semibold text-slate-800">
            <Settings2 className="h-5 w-5 text-red-600" aria-hidden="true" />
            Configuração da ouvidoria
          </h1>
          <AjudaManual artigo="ouvidoria" secao="gestao" />
        </div>
        <p className="text-sm text-slate-500">Prazos legais e regras de aviso. Vale para toda a instância.</p>
      </div>

      <form onSubmit={enviar} className="space-y-5">
        <fieldset className="grid gap-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:grid-cols-2" disabled={!podeEditar}>
          <legend className="px-1 text-sm font-semibold text-slate-700">Prazos</legend>
          {CAMPOS.map((c) => (
            <Campo key={c.chave} label={c.rotulo} htmlFor={`cfg-${c.chave}`} dica={c.dica}>
              <Input
                id={`cfg-${c.chave}`}
                type="number"
                min={c.min}
                max={c.max}
                value={form[c.chave]}
                onChange={(e) => setForm({ ...form, [c.chave]: Number(e.target.value) })}
              />
            </Campo>
          ))}
        </fieldset>

        <fieldset className="space-y-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm" disabled={!podeEditar}>
          <legend className="px-1 text-sm font-semibold text-slate-700">Avisos ao cidadão</legend>
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input
              type="checkbox"
              checked={form.notificarPorWhatsApp}
              onChange={(e) => setForm({ ...form, notificarPorWhatsApp: e.target.checked })}
            />
            Avisar por WhatsApp (recibo, encaminhamento, complementação, prorrogação, resposta, arquivamento)
          </label>
          <p className="text-xs text-slate-500">
            Nunca em manifestação anônima. Em sigilosa, o texto leva só protocolo e etapa — nunca teor ou assunto.
          </p>
          <Campo
            label="Texto do recibo"
            htmlFor="cfg-recibo"
            dica={
              <>
                Em branco usa o padrão. Use <code>{'{protocolo}'}</code> e <code>{'{prazo}'}</code>; são substituídos no envio.
              </>
            }
          >
            <Textarea
              id="cfg-recibo"
              rows={4}
              maxLength={1000}
              value={form.textoRecibo ?? ''}
              onChange={(e) => setForm({ ...form, textoRecibo: e.target.value })}
              placeholder="Sua manifestação foi registrada com o protocolo {protocolo}. Prazo de resposta: {prazo}."
            />
          </Campo>
        </fieldset>

        {erro ? (
          <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {erro}
          </p>
        ) : null}

        {podeEditar ? (
          <div className="flex justify-end">
            <Button type="submit" disabled={salvar.isPending}>
              {salvar.isPending ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <Save className="h-4 w-4" aria-hidden="true" />}
              {salvar.isPending ? 'Salvando…' : 'Salvar'}
            </Button>
          </div>
        ) : (
          <p className="text-xs text-slate-500">Você pode ver a configuração, mas não alterá-la.</p>
        )}
      </form>
    </div>
  );
}
