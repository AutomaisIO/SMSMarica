import { useState } from 'react';
import { Check, Loader2, X } from 'lucide-react';

import { formatarTamanhoBytes } from '@/features/anamnese/lib/anexos';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';

import { AcaoVisualizador, VisualizadorArquivo } from './VisualizadorArquivo';

/** Um arquivo que o servidor já compactou e guarda em memória até a pessoa decidir. */
export type ArquivoPreparado = {
  token: string;
  nome: string;
  contentType: string;
  /** Tamanho do arquivo como foi escolhido; `tamanho` é o que vai ser guardado. */
  tamanhoOriginal: number;
  tamanho: number;
  expiraEm: string;
};

/**
 * Compactação na entrada: o arquivo vai ao servidor, volta compactado numa prévia e só entra quando
 * a pessoa confere que ficou legível. O original não é guardado — por isso a conferência.
 */
export type CompactacaoNaEntrada = {
  preparar: (arquivo: File) => Promise<ArquivoPreparado>;
  caminhoConteudo: (p: ArquivoPreparado) => string;
  aceitar: (p: ArquivoPreparado, titulo: string | null, descricao: string | null) => Promise<void>;
  descartar: (p: ArquivoPreparado) => Promise<void>;
};

type Props = {
  preparado: ArquivoPreparado;
  caminho: string;
  /** O que acontece ao aceitar: "anexar" ou, quando ainda vai pedir o nome, "continuar". */
  rotuloAceitar: string;
  aoAceitar: () => Promise<void>;
  /** Também é o que acontece ao fechar a prévia: sem aceite, nada fica guardado. */
  aoDescartar: () => void;
};

/**
 * A prévia do arquivo compactado, no visualizador do sistema, com a decisão embaixo: está legível
 * (aceita) ou não está (descarta — dá para tirar outra foto ou digitalizar de novo).
 */
export function PreviaCompactacao({ preparado, caminho, rotuloAceitar, aoAceitar, aoDescartar }: Props) {
  const [aceitando, setAceitando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function aceitar() {
    setAceitando(true);
    setErro(null);
    try {
      await aoAceitar();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setAceitando(false);
    }
  }

  return (
    <VisualizadorArquivo
      caminho={caminho}
      titulo={preparado.nome}
      descricao={resumoCompactacao(preparado)}
      nomeArquivo={preparado.nome}
      aoFechar={() => {
        if (!aceitando) aoDescartar();
      }}
      acoes={
        <>
          <p className="mr-auto text-sm text-white/80">
            <strong className="font-semibold text-white">Confira se dá para ler tudo.</strong>{' '}
            {resumoCompactacao(preparado)}
            {erro ? <span className="mt-1 block text-red-300">{erro}</span> : null}
          </p>
          <AcaoVisualizador perigo aoClicar={aoDescartar} disabled={aceitando}>
            <X className="size-4" /> Não está legível — descartar
          </AcaoVisualizador>
          <AcaoVisualizador destaque aoClicar={() => void aceitar()} disabled={aceitando}>
            {aceitando ? <Loader2 className="size-4 animate-spin" /> : <Check className="size-4" />}
            Está legível — {rotuloAceitar}
          </AcaoVisualizador>
        </>
      }
    />
  );
}

/** "Compactado de 4,2 MB para 610 KB." — ou, quando não compensou compactar, que foi como chegou. */
export function resumoCompactacao(p: Pick<ArquivoPreparado, 'tamanho' | 'tamanhoOriginal'>): string {
  return p.tamanho < p.tamanhoOriginal
    ? `Compactado de ${formatarTamanhoBytes(p.tamanhoOriginal)} para ${formatarTamanhoBytes(p.tamanho)}.`
    : `Já estava enxuto e vai como chegou (${formatarTamanhoBytes(p.tamanho)}).`;
}
