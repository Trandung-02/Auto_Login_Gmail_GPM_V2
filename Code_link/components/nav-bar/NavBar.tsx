'use client'

import MenuMobile from '#components/modals/MenuMobile';
import React from 'react';

const NavBar = ({ handleOpendInfoModal }: { handleOpendInfoModal: () => void }) => {

    const [isOpendPrivacyPolicy, setIsOpendPrivacyPolicy] = React.useState(false);
    const [isOpendOther, setIsOpendOther] = React.useState(false);
    const [isOpendMenuMobile, setIsOpendMenuMobile] = React.useState(false);

    const handleOpendMenuMobile = () => {
        setIsOpendMenuMobile(!isOpendMenuMobile);
    }

    const handleOpendPrivacyPolicy = () => {
        handleOpendInfoModal();
    }


    return (
        <div className='w-full min-[769px]:max-w-[360px] max-[768px]:max-w-full overflow-y-auto min-[769px]:h-[100vh] h-[60px] min-[769px]:min-h-auto min-h-[60px] min-[769px]:pt-[28px] px-[20px] pb-5 z-999 min-[769px]:sticky top-0 '>
            <div className='w-full min-[769px]:hidden flex items-center justify-center justify-between gap-2 w-full h-[60px]'>
                <div className='w-[70px]'>
                    <img src="/images/meta/logo-meta.svg" className='w-full' alt="tick" />
                </div>
                <div onClick={handleOpendMenuMobile} className='flex items-center justify-center gap-1 flex-col h-[28px]'>
                    <div className='h-[4px] rounded-[10px] bg-[#344854] w-[28px]'></div>
                    <div className='h-[4px] rounded-[10px] bg-[#344854] w-[28px]'></div>
                    <div className='h-[4px] rounded-[10px] bg-[#344854] w-[28px]'></div>
                </div>
            </div>

            <div className='w-full max-w-[360px] min-[769px]:block hidden'>
                <div className='w-[60px]'>
                    <img src="/images/meta/logo-meta.svg" className='w-full' alt="tick" />
                </div>
                <div className='w-full mt-2 mb-4'>
                    <h1 className='font-bold text-[24px]'>Privacy Center</h1>
                </div>
                <div className='text-[16px] max-w-full w-full'>

                    <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[12px] transition-all duration-200 bg-[#354855] text-white'>
                        <div className='flex items-center justify-center justify-start gap-4'>
                            <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px]'>
                                <img src="/images/icons/ic_home.svg" className='w-full h-full text-white white' alt="home" />
                            </div>
                            <span className='text-white font-[500] text-[15px]'>Privacy Center Home</span>
                        </div>
                    </div>

                    <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[12px] hover:bg-[#E3E8EF] transition-all duration-200'>
                        <div className='flex items-center justify-center justify-start gap-4'>
                            <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px]'>
                                <img src="/images/icons/ic_search.svg" className='w-full h-full' alt="search" />
                            </div>
                            <span className='text-black font-[500] text-[15px]'>Search</span>
                        </div>
                    </div>

                    <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                        <div className='flex items-center justify-center justify-start gap-4'>
                            <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px]'>
                                <img src="/images/icons/ic_setting.svg" className='w-full h-full' alt="other" />
                            </div>
                            <span className='text-black font-[500] text-[15px]'>Commom privacy settings</span>
                        </div>
                    </div>

                    <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                        <div className='flex items-center justify-center justify-start gap-4'>
                            <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px]'>
                                <img src="/images/icons/ic_topics.svg" className='w-full h-full' alt="other" />
                            </div>
                            <span className='text-black font-[500] text-[15px]'>Pivacy topics</span>
                        </div>
                    </div>

                    <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                        <div className='flex items-center justify-center justify-start gap-4'>
                            <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px]'>
                                <img src="/images/icons/ic_more.svg" className='w-full h-full' alt="other" />
                            </div>
                            <span className='text-black font-[500] text-[15px]'>More privacy resources</span>
                        </div>
                    </div>

                    <div>
                        <div onClick={() => setIsOpendPrivacyPolicy(!isOpendPrivacyPolicy)} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                            <div className='flex items-center justify-center justify-start gap-4'>
                                <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px]'>
                                    <img src="/images/icons/ic_locked.svg" className='w-full h-full' alt="locked" />
                                </div>
                                <span className='text-black font-[500] text-[15px]'>Privacy Policy</span>
                            </div>
                            <div className={`w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] transition-all duration-200 ${isOpendPrivacyPolicy ? 'rotate-180' : ''}`}>
                                <img src="/images/icons/ic_arrow.svg" className='w-full h-full' alt="arrow" />
                            </div>
                        </div>

                        <div className={`w-[100%] ${isOpendPrivacyPolicy ? 'block' : 'hidden'}`}>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>What is the Privacy Policy and what does it cover?</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>What information do we collect?</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>How do we use your information?</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>How do we share your information on Meta Products or with integrated partners?</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>How do we share information with third parties?</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>How is the cooperation between Meta Companies organized?</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>How can you manage or delete your information and exercise your rights?</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>How long do we keep your information?</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>How do we transmit information?</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>How do we respond to official requests, comply with applicable laws, and prevent harm?</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>How will you know when the policy changes?</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>How to ask Meta questions?</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>Why and how we process your data</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <div>
                        <div onClick={() => setIsOpendOther(!isOpendOther)} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                            <div className='flex items-center justify-center justify-start gap-4'>
                                <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px]'>
                                    <img src="/images/icons/ic_other.svg" className='w-full h-full' alt="other" />
                                </div>
                                <span className='text-black font-[500] text-[15px]'>Other rules and articles</span>
                            </div>
                            <div className={`w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] transition-all duration-200 ${isOpendOther ? 'rotate-180' : ''}`}>
                                <img src="/images/icons/ic_arrow.svg" className='w-full h-full' alt="arrow" />
                            </div>
                        </div>

                        <div className={`w-[100%] ${isOpendOther ? 'block' : 'hidden'}`}>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-between gap-3 pl-[24px] ml-3 w-full'>
                                    <span className='text-black font-[500] text-[15px]'>Cookie Policy</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-between gap-3 pl-[24px] ml-3 w-full'>
                                    <span className='text-black font-[500] text-[15px]'>Information for those who do not use Meta Products</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'>
                                        <img src="/images/icons/ic_opend.svg" className='w-full h-full' alt="opend" />
                                    </div>
                                </div>
                            </div>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-between gap-3 pl-[24px] ml-3 w-full'>
                                    <span className='text-black font-[500] text-[15px]'>How Meta uses information for generative AI models</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-between gap-3 pl-[24px] ml-3 w-full'>
                                    <span className='text-black font-[500] text-[15px]'>Data Transfer Framework Policy</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div onClick={handleOpendPrivacyPolicy} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px] hover:bg-[#E3E8EF] transition-all duration-200'>
                                <div className='flex items-center justify-between gap-3 pl-[24px] ml-3 w-full'>
                                    <span className='text-black font-[500] text-[15px]'>Other terms and conditions</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'>
                                        <img src="/images/icons/ic_opend.svg" className='w-full h-full' alt="opend" />
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <MenuMobile isOpend={isOpendMenuMobile} onToggleAdvanced={handleOpendMenuMobile} />
        </div>
    )
}

export default NavBar
