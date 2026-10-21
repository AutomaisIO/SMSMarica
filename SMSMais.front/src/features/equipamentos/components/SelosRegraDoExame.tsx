import type { RegraDoExameConfig } from '@/features/equipamentos/types';

const SELO = 'inline-flex items-center rounded px-1.5 py-0.5 text-[11px] font-medium';

const LAUDO: Record<RegraDoExameConfig['laudo'], { texto: string; titulo: string }> = {
  Plataforma: { texto: 'laudo aqui', titulo: 'Laudado e assinado na plataforma: o exame entra na fila de laudo.' },
  JuntoComExame: { texto: 'laudo junto', titulo: 'O exame chega com o laudo pronto, assinado fora, no mesmo PDF.' },
  ForaDaPlataforma: { texto: 'laudo fora', titulo: 'Laudado em outro sistema: não entra na fila de laudo.' },
};

/**
 * Selos da coluna "Depois do exame" nas listas de equipamentos (ADR-0075): onde nasce o laudo,
 * quando o paciente recebe a imagem e se o aparelho dispensa a anamnese — sem abrir o cadastro.
 */
export function SelosRegraDoExame({ equipamento: e }: { equipamento: RegraDoExameConfig }) {
  const laudo = LAUDO[e.laudo] ?? LAUDO.Plataforma;
  const comLaudo = e.liberacaoPaciente === 'ComLaudoAssinado';
  return (
    <span className="inline-flex flex-wrap items-center gap-1">
      <span className={`${SELO} bg-gray-100 text-gray-700`} title={laudo.titulo}>
        {laudo.texto}
      </span>
      <span
        className={`${SELO} ${comLaudo ? 'bg-amber-50 text-amber-800' : 'bg-green-50 text-green-700'}`}
        title={
          comLaudo
            ? 'A imagem fica retida e chega ao paciente junto com o laudo assinado.'
            : 'A imagem chega ao paciente assim que o estudo chega ao PACS.'
        }
      >
        {comLaudo ? 'paciente recebe com o laudo' : 'paciente recebe ao chegar'}
      </span>
      {e.usaAnamnese === false ? (
        <span className={`${SELO} bg-gray-100 text-gray-500`} title="Os exames deste aparelho não usam anamnese.">
          sem anamnese
        </span>
      ) : null}
    </span>
  );
}
