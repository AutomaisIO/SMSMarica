import { useCallback, useEffect, useState } from 'react';
import { CheckCircle2, Star } from 'lucide-react';
import { api, type ViagemParaAvaliar } from '@/lib/api';
import { extrairMensagemDeErro } from '@/lib/httpClient';
import { Card, PrimaryButton } from '@/components/ui';

const ROTULOS = ['', 'Muito ruim', 'Ruim', 'Regular', 'Boa', 'Muito boa'];

/** "2026-10-02" → "02/10" (data sem fuso: vale como está). */
function diaMes(iso: string): string {
  const [, m, d] = iso.split('-');
  return `${d}/${m}`;
}

function Estrelas({ nota, aoEscolher }: { nota: number; aoEscolher?: (n: number) => void }) {
  return (
    <div className="flex gap-1" role={aoEscolher ? 'radiogroup' : undefined} aria-label="Nota da viagem">
      {[1, 2, 3, 4, 5].map((n) => {
        const cheia = n <= nota;
        const icone = <Star className={`h-8 w-8 ${cheia ? 'fill-amber-400 text-amber-400' : 'text-areia'}`} />;
        return aoEscolher ? (
          <button
            key={n}
            type="button"
            role="radio"
            aria-checked={n === nota}
            aria-label={`${n} — ${ROTULOS[n]}`}
            onClick={() => aoEscolher(n)}
            className="rounded-xl p-1 active:bg-papel"
          >
            {icone}
          </button>
        ) : (
          <span key={n} aria-hidden>
            {icone}
          </span>
        );
      })}
    </div>
  );
}

function AvaliarUma({ v, aoAvaliar }: { v: ViagemParaAvaliar; aoAvaliar: () => void }) {
  const [nota, setNota] = useState(0);
  const [comentario, setComentario] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function enviar() {
    setErro(null);
    setEnviando(true);
    try {
      await api.avaliarViagem(v.sessaoId, nota, comentario.trim() || null);
      aoAvaliar();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className="mt-3 space-y-3">
      <Estrelas nota={nota} aoEscolher={setNota} />
      {nota > 0 ? <p className="text-sm text-tinta-mute">{ROTULOS[nota]}</p> : null}
      {nota > 0 ? (
        <>
          <textarea
            value={comentario}
            onChange={(e) => setComentario(e.target.value.slice(0, 500))}
            rows={3}
            placeholder="Quer contar algo? (opcional)"
            className="w-full rounded-2xl border border-areia bg-white px-4 py-3 text-base text-tinta shadow-carta placeholder:text-tinta-mute/40 focus:border-lagoa focus:outline-none focus:ring-4 focus:ring-lagoa/15"
          />
          {erro ? <p role="alert" className="text-sm text-red-700">{erro}</p> : null}
          <PrimaryButton carregando={enviando} onClick={enviar}>
            Enviar avaliação
          </PrimaryButton>
        </>
      ) : null}
    </div>
  );
}

/**
 * Depois da viagem, o paciente avalia o transporte (nota de 1 a 5 e, se quiser, um comentário). Uma vez
 * por viagem, até 30 dias depois. A nota chega à equipe com o motorista e o veículo daquela viagem.
 * Sem viagem recente, a seção não aparece.
 */
export function AvaliarViagens() {
  const [lista, setLista] = useState<ViagemParaAvaliar[] | null>(null);

  const carregar = useCallback(() => {
    api
      .viagensParaAvaliar()
      .then(setLista)
      .catch(() => setLista([])); // avaliação é extra: falha aqui não atrapalha o resto da tela
  }, []);

  useEffect(() => carregar(), [carregar]);

  if (!lista || lista.length === 0) return null;

  return (
    <section className="mt-8">
      <h2 className="mb-1 flex items-center gap-2 font-display text-lg font-semibold text-tinta">
        <Star className="h-5 w-5 text-marica" /> Avalie suas viagens
      </h2>
      <p className="mb-3 text-sm text-tinta-mute">Conte como foi o transporte. Sua nota ajuda a melhorar o serviço.</p>
      <ul className="space-y-2">
        {lista.map((v) => (
          <li key={v.sessaoId}>
            <Card className="p-4">
              <p className="font-semibold text-tinta">
                {diaMes(v.data)} · {v.destino}
              </p>
              {v.motorista ? <p className="text-sm text-tinta-mute">Motorista: {v.motorista}</p> : null}
              {v.nota ? (
                <div className="mt-3 flex items-center gap-3">
                  <Estrelas nota={v.nota} />
                  <span className="inline-flex items-center gap-1 text-sm text-tinta-mute">
                    <CheckCircle2 className="h-4 w-4 text-lagoa" /> Obrigado!
                  </span>
                </div>
              ) : (
                <AvaliarUma v={v} aoAvaliar={carregar} />
              )}
            </Card>
          </li>
        ))}
      </ul>
    </section>
  );
}
