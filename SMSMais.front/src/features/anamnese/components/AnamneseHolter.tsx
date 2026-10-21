import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, HeartPulse, Loader2, Save } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { notificar } from '@/shared/ui/Notificacoes';
import { useAvisoSaidaNaoSalva } from '@/shared/hooks/useAvisoSaidaNaoSalva';
import { useSalvarAnamnese } from '@/features/anamnese/api/queries';
import {
  holterVazio,
  type AnamneseContexto,
  type AnamneseHolterConteudo,
  type RespostaFumante,
} from '@/features/anamnese/types';

const OPCOES_FUMANTE: [RespostaFumante, string][] = [
  ['sim', 'Sim'],
  ['nao', 'Não'],
  ['parou', 'Parou'],
];

/** Lê o conteúdo salvo sem confiar no shape (registro antigo ou editado à mão). */
function lerConteudo(json: string | undefined): AnamneseHolterConteudo {
  if (!json) return holterVazio();
  try {
    const c = JSON.parse(json) as Partial<AnamneseHolterConteudo>;
    return { ...holterVazio(), ...c };
  } catch {
    return holterVazio();
  }
}

/**
 * Anamnese do Holter — altura, peso e fumante, o que o CardioNet pede na worklist. Diferente da
 * mamografia, salvar é o que manda o exame à worklist do aparelho; salvar de novo, corrigido,
 * sobrescreve o paciente lá (o servidor decide e devolve o aviso que aparece aqui).
 */
export function AnamneseHolter({
  ctx,
  janela,
  leitura,
}: {
  ctx: AnamneseContexto;
  janela: boolean;
  leitura: boolean;
}) {
  const navigate = useNavigate();
  const salvar = useSalvarAnamnese();
  const podeEditar = usePermissao('Solicitacoes', 'Edicao') && !janela && !leitura;

  const [conteudo, setConteudo] = useState<AnamneseHolterConteudo>(() => lerConteudo(ctx.anamnese?.conteudoJson));
  const [baseline, setBaseline] = useState(() => JSON.stringify(lerConteudo(ctx.anamnese?.conteudoJson)));
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (janela && ctx.pacienteNome) document.title = `Anamnese — ${ctx.pacienteNome}`;
  }, [janela, ctx.pacienteNome]);

  const completo =
    conteudo.alturaCm !== null && conteudo.pesoKg !== null && conteudo.fumante !== null;

  async function persistir() {
    setErro(null);
    const salvo = await salvar.mutateAsync({
      solicitacaoExameId: ctx.solicitacaoExameId,
      payload: {
        tipo: 'holter',
        versao: 1,
        conteudoJson: JSON.stringify(conteudo),
        classificacaoRisco: null,
      },
    });
    setBaseline(JSON.stringify(conteudo));
    return salvo;
  }

  async function aoSalvar() {
    try {
      const salvo = await persistir();
      notificar(salvo.avisoWorklist ?? 'Anamnese salva com sucesso.');
      navigate(-1);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const sujo = podeEditar && JSON.stringify(conteudo) !== baseline;
  const { elemento: modalSaida, protegerAcao } = useAvisoSaidaNaoSalva({
    sujo,
    aoSalvar: async () => {
      await persistir();
    },
    mensagem: 'A anamnese tem alterações que ainda não foram salvas. Deseja salvar antes de sair?',
  });

  function numero(valor: string, inteiro: boolean): number | null {
    if (valor.trim() === '') return null;
    const n = Number(valor.replace(',', '.'));
    if (!Number.isFinite(n)) return null;
    return inteiro ? Math.round(n) : n;
  }

  const campo =
    'mt-1 w-40 rounded-md border border-gray-300 px-3 py-2 text-sm disabled:bg-gray-100 disabled:text-gray-600';

  return (
    <div className={`mx-auto max-w-3xl space-y-6 pb-10 ${janela ? 'min-h-screen bg-gray-50 p-6' : ''}`}>
      {modalSaida}
      <div>
        {janela ? null : (
          <button
            type="button"
            onClick={protegerAcao(() => navigate(-1))}
            className="inline-flex items-center gap-1 text-sm text-gray-600 hover:text-gray-900"
          >
            <ArrowLeft className="h-4 w-4" /> Voltar
          </button>
        )}
        <h1 className="mt-1 flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <HeartPulse className="h-6 w-6 text-primary-600" />
          Anamnese do Holter
        </h1>
        <p className="text-sm text-gray-500">
          {ctx.pacienteNome} · Pedido <span className="font-mono">{ctx.accessionNumber}</span> ·{' '}
          {ctx.tipoExameNome}
          {ctx.anamnese?.preenchidoPorNome ? <> · preenchida por {ctx.anamnese.preenchidoPorNome}</> : null}
        </p>
      </div>

      <div className="rounded-md border border-sky-200 bg-sky-50 px-3 py-2 text-sm text-sky-900">
        Estes dados vão para a worklist do aparelho (CardioNet). O exame só aparece lá depois que a
        anamnese for salva. Se algo estiver errado, abra de novo, corrija e salve: o paciente é
        atualizado no aparelho.
      </div>

      <div className="space-y-5 rounded-lg border border-gray-200 bg-white p-5">
        <div className="flex flex-wrap gap-6">
          <label className="block text-sm font-medium text-gray-700">
            Altura (cm)
            <input
              type="number"
              inputMode="numeric"
              min={50}
              max={250}
              step={1}
              disabled={!podeEditar}
              value={conteudo.alturaCm ?? ''}
              onChange={(e) => setConteudo((c) => ({ ...c, alturaCm: numero(e.target.value, true) }))}
              className={campo}
            />
          </label>
          <label className="block text-sm font-medium text-gray-700">
            Peso (kg)
            <input
              type="number"
              inputMode="decimal"
              min={2}
              max={400}
              step={0.1}
              disabled={!podeEditar}
              value={conteudo.pesoKg ?? ''}
              onChange={(e) => setConteudo((c) => ({ ...c, pesoKg: numero(e.target.value, false) }))}
              className={campo}
            />
          </label>
        </div>

        <fieldset>
          <legend className="text-sm font-medium text-gray-700">Fumante</legend>
          <div className="mt-2 flex gap-4">
            {OPCOES_FUMANTE.map(([valor, rotulo]) => (
              <label key={valor} className="inline-flex items-center gap-2 text-sm text-gray-800">
                <input
                  type="radio"
                  name="fumante"
                  disabled={!podeEditar}
                  checked={conteudo.fumante === valor}
                  onChange={() => setConteudo((c) => ({ ...c, fumante: valor }))}
                />
                {rotulo}
              </label>
            ))}
          </div>
        </fieldset>
      </div>

      {erro ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}

      {podeEditar ? (
        <div className="flex justify-end">
          <Button onClick={aoSalvar} disabled={salvar.isPending || !completo}>
            {salvar.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            {ctx.anamnese ? 'Salvar correção' : 'Salvar e enviar à worklist'}
          </Button>
        </div>
      ) : null}
    </div>
  );
}
