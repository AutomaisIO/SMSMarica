import { useCallback, useEffect, useState } from 'react';
import { Star } from 'lucide-react';
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
      await api.avaliarViagem(v.sessaoId, v.trecho, nota, comentario.trim() || null);
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

/** Hoje, em Brasília, como "aaaa-mm-dd" (o mesmo formato da data da viagem). */
function hoje(): string {
  return new Date().toLocaleDateString('sv-SE', { timeZone: 'America/Sao_Paulo' });
}

/**
 * Avaliação do motorista (decisão de 19/10): ao desembarcar na clínica o paciente avalia o motorista da
 * ida, e ao chegar em casa, o do resgate — até 5 estrelas, cada um uma vez, até 30 dias depois. Fica no
 * topo do Transporte e atualiza a cada minuto, para o pedido aparecer logo depois do desembarque. A nota
 * chega à equipe com o motorista e o veículo daquele trecho. Sem nada a avaliar, não aparece.
 */
export function AvaliarViagens() {
  const [lista, setLista] = useState<ViagemParaAvaliar[] | null>(null);

  const carregar = useCallback(() => {
    api
      .viagensParaAvaliar()
      .then(setLista)
      .catch(() => setLista([])); // avaliação é extra: falha aqui não atrapalha o resto da tela
  }, []);

  useEffect(() => {
    carregar();
    const t = window.setInterval(carregar, 60_000);
    return () => window.clearInterval(t);
  }, [carregar]);

  const pendentes = (lista ?? []).filter((v) => v.nota == null);
  if (pendentes.length === 0) return null;
  const dia = hoje();

  return (
    <section className="mb-6">
      <h2 className="mb-1 flex items-center gap-2 font-display text-lg font-semibold text-tinta">
        <Star className="h-5 w-5 text-marica" /> Como foi o motorista?
      </h2>
      <p className="mb-3 text-sm text-tinta-mute">Sua nota ajuda a melhorar o transporte.</p>
      <ul className="space-y-2">
        {pendentes.map((v) => (
          <li key={`${v.sessaoId}:${v.trecho}`}>
            <Card className={`p-4 ${v.data === dia ? 'border-2 border-lagoa/40' : ''}`}>
              <p className="font-semibold text-tinta">
                {v.trecho === 'Resgate' ? `Volta para casa · ${v.destino}` : `Ida até ${v.destino}`}
              </p>
              <p className="text-sm text-tinta-mute">
                {v.data === dia ? 'Hoje' : diaMes(v.data)}
                {v.hora ? `, ${v.trecho === 'Resgate' ? 'chegou em casa' : 'chegou'} às ${v.hora.slice(0, 5)}` : ''}
                {v.motorista ? ` · Motorista: ${v.motorista}` : ''}
              </p>
              <AvaliarUma v={v} aoAvaliar={carregar} />
            </Card>
          </li>
        ))}
      </ul>
    </section>
  );
}
