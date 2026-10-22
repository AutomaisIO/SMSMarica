import { useCallback, useEffect, useState } from 'react';
import { Car, CheckCircle2, Clock } from 'lucide-react';
import { api, type ResgateDoPaciente } from '@/lib/api';
import { extrairMensagemDeErro } from '@/lib/httpClient';
import { Card, PrimaryButton } from '@/components/ui';

function hhmm(t: string | null): string | null {
  return t ? t.slice(0, 5) : null;
}

function horaDe(iso: string | null): string | null {
  return iso ? new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }) : null;
}

/**
 * O resgate de hoje: depois do atendimento, o carro busca o paciente na unidade e o leva para casa. Quando
 * estoura a previsão do procedimento, a tela pergunta "você já está disponível para retorno?": "Sim" põe o
 * paciente no topo da lista dos motoristas; "Não, ainda não terminei" pergunta de novo em 10 minutos. Antes
 * disso, quem terminou antes do previsto avisa sozinho ("Já terminei, estou disponível para retorno"). Quando um carro marca, ele vê qual vai buscá-lo. Some
 * quando não há resgate hoje.
 */
export function AguardarResgate() {
  const [resgate, setResgate] = useState<ResgateDoPaciente | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const carregar = useCallback(() => {
    api
      .resgate()
      .then(setResgate)
      .catch(() => setResgate(null)); // falha aqui não atrapalha o resto da tela
  }, []);

  useEffect(() => {
    carregar();
    const t = window.setInterval(carregar, 60_000);
    return () => window.clearInterval(t);
  }, [carregar]);

  if (!resgate) return null;

  async function responder(disponivel: boolean) {
    if (!resgate) return;
    setErro(null);
    setEnviando(true);
    try {
      await api.responderResgate(resgate.sessaoId, disponivel);
      carregar();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <section className="mb-6">
      <Card className="border-2 border-lagoa/40 p-4">
        <p className="flex items-center gap-2 font-display text-lg font-semibold text-tinta">
          <Car className="h-5 w-5 text-marica" /> Seu resgate de hoje
        </p>
        <p className="mt-1 text-sm text-tinta-mute">{resgate.unidade}</p>

        {resgate.embarcou ? (
          <p className="mt-3 inline-flex items-center gap-1.5 text-sm text-tinta">
            <CheckCircle2 className="h-4 w-4 text-lagoa" />
            Você está a caminho de casa{resgate.veiculoPlaca ? ` no carro ${resgate.veiculoPlaca}` : ''}.
          </p>
        ) : (
          <>
            {resgate.veiculoPlaca ? (
              <p className="mt-3 text-sm font-semibold text-tinta">
                O carro {resgate.veiculoPlaca}
                {resgate.veiculoModelo ? ` (${resgate.veiculoModelo})` : ''} vai buscar você.
              </p>
            ) : null}
            {resgate.disponibilidade === 'NoLocal' ? (
              <p className="mt-3 inline-flex items-center gap-1.5 text-sm text-tinta">
                <CheckCircle2 className="h-4 w-4 text-lagoa" />
                Os motoristas já sabem que você está esperando
                {resgate.noLocalDesde ? ` (desde ${horaDe(resgate.noLocalDesde)})` : ''}. Aguarde no local.
              </p>
            ) : resgate.perguntaPendente ? (
              <>
                <p className="mt-3 font-semibold text-tinta">Você já está disponível para retorno?</p>
                {erro ? <p role="alert" className="mt-2 text-sm text-red-700">{erro}</p> : null}
                <div className="mt-3 space-y-2">
                  <PrimaryButton carregando={enviando} onClick={() => responder(true)}>
                    Sim, já estou disponível
                  </PrimaryButton>
                  <button
                    type="button"
                    disabled={enviando}
                    onClick={() => responder(false)}
                    className="w-full rounded-2xl border border-areia bg-white px-4 py-3 text-base font-semibold text-tinta active:bg-papel disabled:opacity-50"
                  >
                    Não, ainda não terminei o procedimento
                  </button>
                </div>
              </>
            ) : (
              <>
                {resgate.proximaPerguntaAs ? (
                  <p className="mt-3 inline-flex items-center gap-1.5 text-sm text-tinta-mute">
                    <Clock className="h-4 w-4" /> Perguntamos de novo às {horaDe(resgate.proximaPerguntaAs)}.
                  </p>
                ) : hhmm(resgate.disponivelAs) ? (
                  <p className="mt-3 inline-flex items-center gap-1.5 text-sm text-tinta-mute">
                    <Clock className="h-4 w-4" /> Previsão de saída do atendimento: {hhmm(resgate.disponivelAs)}
                  </p>
                ) : null}
                <p className="mt-2 text-sm text-tinta-mute">
                  Terminou o atendimento? Avise, e os motoristas veem na hora que você está esperando.
                </p>
                {erro ? <p role="alert" className="mt-2 text-sm text-red-700">{erro}</p> : null}
                <div className="mt-3">
                  <PrimaryButton carregando={enviando} onClick={() => responder(true)}>
                    Já terminei, estou disponível para retorno
                  </PrimaryButton>
                </div>
              </>
            )}
          </>
        )}
      </Card>
    </section>
  );
}
