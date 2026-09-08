import { useState } from 'react';
import { FlaskConical, Loader2, Sparkles } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';

import { obterSementeFollowUp, testarFollowUp } from '../../api/regulacaoApi';
import type { TesteFollowUp } from '../../types';

/**
 * Editor das regras de follow-up (plano 09, tarefa 4.6).
 *
 * <p><b>Por que um editor de JSON e não um formulário:</b> as regras são regex — a maior tem 601
 * caracteres, com alternativas, classes e um lookahead negativo. Um formulário campo a campo
 * esconderia justamente o que precisa ser lido inteiro para se entender.</p>
 *
 * <p><b>Por que a caixa de teste é obrigatória aqui:</b> o consumidor destas regras é a varredura
 * noturna do SER/SERNIT. Sem passar um texto real por elas na hora da edição, um erro só aparece
 * de madrugada, classificando follow-up de paciente — e a categoria decide se a unidade recebe
 * uma pendência ou não.</p>
 */
export function EditorRegrasFollowUp({
  valor,
  aoMudar,
}: {
  valor: unknown;
  aoMudar: (v: unknown) => void;
}) {
  const [texto, setTexto] = useState(() => JSON.stringify(valor ?? [], null, 2));
  const [erroJson, setErroJson] = useState<string | null>(null);
  const [amostra, setAmostra] = useState('');
  const [resultado, setResultado] = useState<TesteFollowUp | null>(null);
  const [erroTeste, setErroTeste] = useState<string | null>(null);
  const [testando, setTestando] = useState(false);
  const [carregandoSemente, setCarregandoSemente] = useState(false);

  function editar(novo: string) {
    setTexto(novo);
    try {
      const parseado: unknown = JSON.parse(novo);
      if (!Array.isArray(parseado)) {
        setErroJson('As regras devem ser uma lista.');
        return;
      }
      setErroJson(null);
      aoMudar(parseado);
    } catch {
      // Enquanto o JSON está pela metade, o formulário guarda o último valor válido: assim
      // digitar não apaga o que já estava salvo caso a pessoa desista no meio.
      setErroJson('JSON inválido — o salvamento vai usar o último conteúdo válido.');
    }
  }

  async function carregarSemente() {
    setCarregandoSemente(true);
    try {
      const semente = await obterSementeFollowUp();
      const formatado = JSON.stringify(semente, null, 2);
      setTexto(formatado);
      setErroJson(null);
      aoMudar(semente);
    } catch (e) {
      setErroJson(extrairMensagemDeErro(e));
    } finally {
      setCarregandoSemente(false);
    }
  }

  async function testar() {
    setErroTeste(null);
    setTestando(true);
    try {
      setResultado(await testarFollowUp(amostra));
    } catch (e) {
      setErroTeste(extrairMensagemDeErro(e));
    } finally {
      setTestando(false);
    }
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <Button
          variante="secundaria"
          type="button"
          onClick={() => void carregarSemente()}
          disabled={carregandoSemente}
        >
          {carregandoSemente ? (
            <Loader2 className="mr-2 size-4 animate-spin" />
          ) : (
            <Sparkles className="mr-2 size-4" />
          )}
          Carregar as regras medidas nos follow-ups reais
        </Button>
        <span className="text-xs text-slate-500">
          Substitui o conteúdo abaixo. Nada é aplicado até você salvar.
        </span>
      </div>

      <textarea
        value={texto}
        onChange={(e) => editar(e.target.value)}
        spellCheck={false}
        rows={14}
        className="w-full rounded-md border border-slate-300 bg-slate-50 p-3 font-mono text-xs text-slate-800 focus:border-red-500 focus:outline-none"
      />
      {erroJson ? <p className="text-xs text-amber-700">{erroJson}</p> : null}

      <div className="rounded-md border border-slate-200 bg-white p-3">
        <p className="text-sm font-medium text-slate-700">Testar um texto</p>
        <p className="mb-2 text-xs text-slate-500">
          Usa as regras <strong>já salvas</strong>. Edite, salve, depois teste — é assim que se vê
          o efeito de uma alteração antes da varredura da madrugada.
        </p>
        <div className="flex flex-wrap gap-2">
          <input
            value={amostra}
            onChange={(e) => setAmostra(e.target.value)}
            placeholder="Cole aqui um follow-up real"
            className="min-w-0 flex-1 rounded-md border border-slate-300 px-3 py-1.5 text-sm focus:border-red-500 focus:outline-none"
          />
          <Button type="button" variante="secundaria" onClick={() => void testar()} disabled={testando}>
            {testando ? (
              <Loader2 className="mr-2 size-4 animate-spin" />
            ) : (
              <FlaskConical className="mr-2 size-4" />
            )}
            Testar
          </Button>
        </div>

        {erroTeste ? <p className="mt-2 text-xs text-red-700">{erroTeste}</p> : null}

        {resultado ? (
          <div className="mt-3 space-y-1 rounded border border-slate-200 bg-slate-50 p-2 text-xs text-slate-700">
            <p>
              Categoria: <strong>{resultado.categoria}</strong>
              {resultado.ordemDaRegra !== null ? ` (regra de ordem ${resultado.ordemDaRegra})` : ''}
            </p>
            <p>
              {resultado.viraPendencia
                ? `Abre pendência de ${resultado.viraPendencia} na unidade solicitante.`
                : 'Não abre pendência — vira informação na linha do tempo.'}
            </p>
            <p className="text-slate-500">
              Como o classificador lê: <span className="font-mono">{resultado.textoNormalizado}</span>
            </p>
          </div>
        ) : null}
      </div>
    </div>
  );
}
