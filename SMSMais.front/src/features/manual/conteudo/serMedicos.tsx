import { Stethoscope } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P } from '@/features/manual/components/Prosa';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo de Regulação → SER → Médicos (ADR-0065): o espelho dos profissionais do SER, à parte do
 * nosso cadastro de Médicos.
 *
 * Conferido no código em 01/10/2026: `features/ser/pages/SerMedicosPage`, `features/ser/api/
 * profissionaisApi`, `SerProfissionaisController`, `SerProfissionalService`,
 * `SerProfissionalLeitor`.
 */
export const artigoSerMedicos: Artigo = {
  slug: 'ser-medicos',
  titulo: 'Médicos do SER',
  resumo:
    'Os médicos como estão cadastrados no SER, num espelho à parte do nosso cadastro — de onde sai o “Médico solicitante” das solicitações ao SER.',
  grupo: 'regulacao',
  icone: Stethoscope,
  rota: '/app/regulacao/ser/medicos',
  publico: 'Quem prepara as solicitações ao SER e cuida da lista de médicos solicitantes',
  atualizadoEm: '2026-10-01',
  palavrasChave: [
    'médicos do SER',
    'profissionais',
    'cadastro de profissionais',
    'médico solicitante',
    'lotação',
    'importar do SER',
    'ligar a médico nosso',
    'desligar',
    'saiu do SER',
    'duplicado',
    'CPF',
    'CRM',
    'documento',
    'espelho',
    'enviar para o SER',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'O que é esta tela',
      busca: 'médicos do ser espelho à parte cadastro médico solicitante lista lotação município',
      conteudo: (
        <>
          <P>
            O SER só aceita como <strong>médico solicitante</strong> quem está cadastrado lá, com
            lotação no município. Esta tela mostra esses médicos <strong>exatamente como o SER os
            tem</strong> — e é dessa mesma lista que sai o campo “Médico solicitante” das solicitações
            ao SER.
          </P>
          <Callout tipo="regra" titulo="À parte do nosso cadastro">
            O cadastro do SER é ruim: quase ninguém tem CPF, metade só tem o nome, há nomes abreviados e
            cadastros repetidos. Por isso ele fica separado — nada daqui muda o nosso cadastro de
            Médicos, e nada do nosso é copiado para cá.
          </Callout>
        </>
      ),
    },
    {
      id: 'importar',
      titulo: 'Importar do SER',
      busca: 'importar do ser leitura pesquisa minutos lendo o ser última leitura falhou saiu do ser',
      conteudo: (
        <>
          <P>
            <BotaoRef>Importar do SER</BotaoRef> lê a pesquisa de profissionais do SER inteira — leva um
            ou dois minutos, e o botão mostra “Lendo o SER…” enquanto isso. É só leitura: nada é
            gravado no SER.
          </P>
          <Lista>
            <Item>Quem é novo no SER entra na lista.</Item>
            <Item>
              Quem não aparece mais na pesquisa fica marcado <SeloRef>Saiu do SER</SeloRef> — não é
              apagado, e a ligação com o nosso médico continua.
            </Item>
            <Item>
              Se a leitura voltar muito menor do que a anterior (a sessão caiu, a tela mudou), nada é
              alterado e a tela avisa que a importação falhou.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'lista',
      titulo: 'A lista',
      busca: 'cartões no ser ativos com cpf ligados saíram buscar nome cpf documento filtro situação ligação repetido',
      conteudo: (
        <>
          <P>
            Os cartões do alto contam os médicos no SER, os ativos, os que têm CPF lá, os já ligados a
            um médico nosso e os que saíram do SER. A busca aceita nome (sem exigir acento), CPF ou
            documento.
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Nome no SER', descricao: 'Como o SER escreve. “2× no SER” = o SER tem esse cadastro repetido.' },
              { termo: 'CPF / Documento', descricao: 'O que o SER tem — em geral vazio. O documento costuma ser o CRM.' },
              {
                termo: 'Situação',
                descricao: (
                  <>
                    <SeloRef cor="sucesso">Ativo</SeloRef> aparece na lista de médicos da solicitação;{' '}
                    <SeloRef>Inativo</SeloRef> não.
                  </>
                ),
              },
              { termo: 'Médico nosso', descricao: 'O médico do nosso cadastro que alguém confirmou ser o mesmo.' },
            ]}
          />
        </>
      ),
    },
    {
      id: 'ligar',
      titulo: 'Ligar a um médico nosso',
      busca: 'ligar a médico nosso é este desligar mesmo médico confirmar homônimo cpf crm',
      conteudo: (
        <>
          <P>
            <BotaoRef>Ligar a médico nosso</BotaoRef> abre a busca no nosso cadastro já com o nome (ou o
            CPF) do SER. Confira CPF, CRM e especialidade e clique em <BotaoRef>É este</BotaoRef>. Errou?
            <BotaoRef>Desligar</BotaoRef> desfaz.
          </P>
          <Callout tipo="atencao" titulo="Quem liga é a pessoa">
            O sistema não liga sozinho: no SER há homônimos e nomes abreviados demais para casar pelo
            nome. Na dúvida, não ligue.
          </Callout>
        </>
      ),
    },
    {
      id: 'enviar',
      titulo: 'E o médico que não está no SER?',
      busca: 'enviar para o ser cadastrar médico no ser lotação especialidade cbo não liberado autorização',
      conteudo: (
        <P>
          Por enquanto, ele precisa ser cadastrado no próprio SER (Cadastro → Profissionais). Enviar
          pela nossa plataforma está desenhado, mas ainda não liberado: é escrita no sistema do Estado e
          precisa de autorização antes do primeiro envio. Depois de cadastrar lá, use{' '}
          <BotaoRef>Importar do SER</BotaoRef> para ele aparecer aqui e na solicitação.
        </P>
      ),
    },
  ],
};
