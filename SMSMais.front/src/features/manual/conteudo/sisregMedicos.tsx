import { Stethoscope } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { P } from '@/features/manual/components/Prosa';
import { AbaRef, BotaoRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo de Regulação → SISREG → Médicos. Conferido no código em 02/10/2026:
 * `features/sisreg/pages/SisregMedicosPage`, `features/regulacao/components/MedicoSisreg`,
 * `RegulacaoMedicoLocalService` e `ConsolidacaoMedicos` (as regras de junção).
 */
export const artigoSisregMedicos: Artigo = {
  slug: 'sisreg-medicos',
  titulo: 'Médicos do SISREG',
  resumo:
    'O nosso cadastro de médicos solicitantes do SISREG: montado das fichas, sem repetição, para a Nova Solicitação preencher CPF e nome.',
  grupo: 'regulacao',
  icone: Stethoscope,
  rota: '/app/sisreg/medicos',
  publico: 'Quem prepara solicitações ao SISREG e quem cuida da regulação',
  atualizadoEm: '2026-10-02',
  palavrasChave: [
    'médicos do SISREG',
    'médico solicitante',
    'profissional solicitante',
    'CPF do médico',
    'incluir médico',
    'possíveis repetidos',
    'juntar',
    'grafias',
    'ler as fichas',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'cadastro nosso sisreg não tem lista de médicos texto digitado cpf nome ficha nova solicitação preencher',
      conteudo: (
        <>
          <P>
            O SISREG não tem lista de médicos: cada ficha leva o CPF e o nome do profissional
            solicitante, digitados por quem abriu o pedido. Este cadastro é <strong>nosso</strong>: junta
            os médicos que as fichas importadas já trouxeram, para a Nova Solicitação aproveitar em vez
            de redigitar. Nada daqui é escrito no SISREG: a solicitação continua levando CPF e nome como
            texto.
          </P>
        </>
      ),
    },
    {
      id: 'sem-repeticao',
      titulo: 'Como o mesmo médico vira um só',
      busca:
        'repetido duplicado grafia abreviado erro de digitação mesmo cpf junta sozinho nome mais completo primeiro nome só',
      conteudo: (
        <>
          <P>
            Nas fichas, o mesmo médico aparece de vários jeitos: “ANA MARIA SOUZA”, “Ana M. Souza”, “ANA
            MARIA DE SOUZA”. A leitura junta sozinha só o que é seguro:
          </P>
          <ul className="list-disc space-y-1 pl-5 text-sm text-slate-700">
            <li>a mesma escrita depois de tirar acento, pontuação e “de/da/dos”;</li>
            <li>o mesmo CPF, quando o primeiro nome também bate;</li>
            <li>
              a abreviação de um único nome mais completo, com 3 palavras ou mais e primeiro e último nome
              por extenso. Uma letra trocada só conta em palavra comprida (“FRANCICO” é “FRANCISCO”, mas
              “MARTA” não é “MARIA”).
            </li>
          </ul>
          <P>
            O nome que fica é a forma mais completa; as outras aparecem embaixo, em “também escrito”, e a
            busca acha por qualquer uma delas. Cadastros com CPFs diferentes nunca são juntados: são duas
            pessoas.
          </P>
          <Callout tipo="regra" titulo="Ficha com só o primeiro nome fica de fora">
            Muitas fichas antigas trazem só “CLAUDIA” ou “CARLOS”, sem CPF nem conselho. Não há como saber
            quem é: esses nomes não entram no cadastro.
          </Callout>
        </>
      ),
    },
    {
      id: 'tela',
      titulo: 'A tela',
      busca: 'busca por palavras cpf corrigir lápis nome antigo continua achando ler as fichas de novo pedidos',
      conteudo: (
        <>
          <P>
            Na aba <AbaRef>Médicos</AbaRef>, sem busca aparecem os mais usados primeiro. Procure por
            palavras do nome, em qualquer ordem (acha abreviado), ou pelo começo do CPF. Cada linha mostra
            CPF, conselho e quantos pedidos as fichas têm com aquele médico.
          </P>
          <P>
            O lápis corrige nome, CPF e conselho. O nome antigo continua achando o médico. O CPF é
            conferido: não aceita CPF inválido nem o CPF que já é de outro cadastro (nesse caso, junte os
            dois).
          </P>
          <P>
            <BotaoRef>Ler as fichas de novo</BotaoRef> relê as fichas importadas e acrescenta quem falta:
            médicos novos, grafias novas, e o CPF ou o conselho de quem ainda não tinha. Não repete
            ninguém, não sobrescreve o que alguém corrigiu à mão e não desfaz o que foi juntado. Não faz
            nenhuma consulta ao SISREG: só lê o que já foi importado.
          </P>
        </>
      ),
    },
    {
      id: 'repetidos',
      titulo: 'Possíveis repetidos',
      busca: 'possíveis repetidos juntar fica este confirmar deixa de existir ambíguo',
      conteudo: (
        <>
          <P>
            O que a leitura não juntou sozinha, por não ter certeza, aparece em{' '}
            <AbaRef>Possíveis repetidos</AbaRef>. Exemplo: “ANA M SOUZA” pode ser a ANA MARIA ou a ANA
            MARTA. Se os dois forem o mesmo médico, clique em <BotaoRef>Fica este</BotaoRef> no nome certo e
            confirme. O outro cadastro deixa de existir, e as grafias e os pedidos dele passam para o que
            ficou.
          </P>
          <Callout tipo="atencao" titulo="Juntar não se desfaz">
            Na dúvida, não junte: um médico repetido só atrapalha a busca, mas dois médicos juntados levam
            o CPF de um para o pedido do outro.
          </Callout>
        </>
      ),
    },
    {
      id: 'nova-solicitacao',
      titulo: 'Na Nova Solicitação',
      busca: 'procurar médico já usado no sisreg incluir médico conferir se já existe é este nenhum destes',
      conteudo: (
        <P>
          Na Nova Solicitação com destino SISREG, <strong>Procurar médico já usado no SISREG</strong> fica
          abaixo do CPF do profissional, e escolher preenche CPF e nome. <BotaoRef>Não achou? Incluir
          médico</BotaoRef> confere antes se o médico já existe (CPF igual ou nome parecido) e grava direto
          aqui, sem pendência, porque não há o que cadastrar no SISREG.
        </P>
      ),
    },
  ],
};
